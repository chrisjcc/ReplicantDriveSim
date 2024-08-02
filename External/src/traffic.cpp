#include "traffic.h"
#include "perception_module.h"

#include <iostream>
#include <algorithm> // for std::max and std::min
#include <cmath>
#include <random>


// Include order: standard headers, then local headers
#include "Lane.h"
#include "LaneSection.h"
#include "Math.hpp"
#include "Mesh.h"
#include "OpenDriveMap.h"
#include "RoadNetworkMesh.h"
#include "Road.h"

const int SCREEN_WIDTH = 900;
const int VEHICLE_WIDTH = 130;
const int LANE_WIDTH = 25;

// Custom clamp function for C++11
template <typename T>
T clamp(T value, T min_val, T max_val) {
    return std::max(min_val, std::min(value, max_val));
}

// Helper function to calculate distance between two Vec3D points
float distance(const odr::Vec3D& a, const odr::Vec3D& b) {
    return std::sqrt(std::pow(a[0] - b[0], 2) + std::pow(a[1] - b[1], 2) + std::pow(a[2] - b[2], 2));
}

/**
 * @brief Constructor for Traffic.
 * @param num_agents Number of agents (vehicles) in the simulation.
 */
Traffic::Traffic(int num_agents, const std::string& map_file) : odr_map(std::make_shared<odr::OpenDriveMap>(map_file)), num_agents(num_agents) {
    seed = 314; //42;
    generator.seed(seed), // Initialize the generator with the seed

    agents.resize(num_agents);
    previous_positions.resize(num_agents);

    perceptionModule = std::make_unique<PerceptionModule>(*this); // Initialize the pointer

    // Get all roads from the OpenDRIVE map
    std::vector<odr::Road> roads = odr_map->get_roads();

    // Randomly select a road
    const odr::Road& road = roads[rand() % roads.size()];

    // Randomly select a lane section
    std::vector<odr::LaneSection> lane_sections = road.get_lanesections();
    size_t lane_section_index = rand() % lane_sections.size();
    const odr::LaneSection& lane_section = lane_sections[lane_section_index];

    // Determine the start and end position of the lane section
    float s0 = lane_section.s0;
    float s1 = (lane_section_index == lane_sections.size() - 1) ? road.length : lane_sections[lane_section_index + 1].s0;

    // Get drivable lanes (excluding sidewalks, etc.)
    std::vector<const odr::Lane*> drivable_lanes;

    for (const auto& lane_pair : lane_section.id_to_lane) {
        const odr::Lane* lane = &lane_pair.second;

        if (lane->type == "driving" || lane->type == "exit" || lane->type == "entry") {
            drivable_lanes.push_back(lane);
        }
    }

    // Randomly select a drivable lane
    const odr::Lane* lane = drivable_lanes[rand() % drivable_lanes.size()];

    // Sample a position along the lane
    float s = s0 + static_cast<float>(rand()) / static_cast<float>(RAND_MAX) * (s1 - s0);

    // Calculate t coordinate (assuming lane center)
    float t = 0.0f;

    for (const auto& lane_pair : lane_section.id_to_lane) {
        const odr::Lane* current_lane = &lane_pair.second;

        if (current_lane == lane) {
            t += 0.5f * current_lane->lane_width.get(s); // Center of the lane
            break;
        }
        if (lane->id > 0) {
            t += current_lane->lane_width.get(s);
        } else {
            t -= current_lane->lane_width.get(s);
        }
    }

    // Convert lane coordinates to world coordinates
    odr::Vec3D position = road.get_xyz(s, t, 0.0);

    // Get the heading direction at position s on the lane
    float heading = get_heading(position);

    // Initialize agents with random positions and attributes
    for (int i = 0; i < num_agents; ++i) {
        agents[i].setId(i);
        agents[i].setName("agent_" + std::to_string(i));
        agents[i].setWidth(2.0f);
        agents[i].setLength(5.0f);
        agents[i].setSensorRange(200.0f);
        agents[i].setX(randFloat(-0.5 * (LANE_WIDTH - 0.5 * agents[i].getWidth()), 0.5 * (LANE_WIDTH - agents[i].getWidth())));
        agents[i].setY(0.0f);
        agents[i].setZ(randFloat(0.0f, 4.0f * agents[i].getLength()));
        agents[i].setVx(randNormal(0.0f, 0.5f));  // Initial lateral speed
        agents[i].setVy(0.0f);  // Initial verticle speed
        agents[i].setVz(randNormal(50.0f, 2.0f)); // Initial longitudinal speed
        agents[i].setSteering(clamp(randNormal(heading, 1.0f), -0.610865f, 0.610865f)); // +/- 35 degrees (in rad)

        // Initialize previous positions with current positions
        previous_positions[i] = agents[i];
    }
}

/**
 * @brief Initializes the position of a specific agent.
 * @param agent_index Index of the agent to be initialized.
 */
/*
void Traffic::initializeAgentPosition(int agent_index) {

}
*/

/**
 * @brief Destructor for Traffic.
 * Cleans up memory allocated for perception module.
 */
Traffic::~Traffic() {
    // No need to delete perceptionModule explicitly; std::unique_ptr handles it
}

/**
 * @brief Performs a simulation step.
 * Updates agent positions based on high-level and low-level actions,
 * updates perceptions, and checks for collisions.
 * @param high_level_actions High-level actions for each agent.
 * @param low_level_actions Low-level actions for each agent.
 */
void Traffic::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {
    // Update positions of all agents
    for (auto& agent : agents) {
        updatePosition(agent, high_level_actions[agent.getId()], low_level_actions[agent.getId()]);
    }

    // Update perceptions
    perceptionModule->updatePerceptions();

    // Check for collisions between agents
    checkCollisions(); // Collision detection
}

/**
 * @brief Retrieves all agents in the simulation.
 * @return Vector of all agents.
 */
const std::vector<Vehicle>& Traffic::get_agents() const {
    return agents;
}

/**
 * @brief Retrieves an agent by its name.
 * @param name The name of the agent to retrieve.
 * @return Reference to the agent.
 * @throws std::runtime_error if the agent with the given name is not found.
 */
Vehicle& Traffic::get_agent_by_name(const std::string& name) {
    auto it = std::find_if(agents.begin(), agents.end(),
                           [&name](const Vehicle& agent) {
                               return agent.getName() == name;
                           });

    if (it != agents.end()) {
        return *it;
    } else {
        throw std::runtime_error("Agent with name " + name + " not found.");
    }
}

/**
 * @brief Retrieves positions of all agents.
 * @return Unordered map where keys are agent names and values are positions.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::get_agent_positions() const {
    std::unordered_map<std::string, std::vector<float>> positions;
    for (int i = 0; i < num_agents; ++i) {
        positions["agent_" + std::to_string(i)] = {agents[i].getX(), agents[i].getY(), agents[i].getZ()};
    }
    return positions;
}

/**
 * @brief Retrieves velocities of all agents.
 * @return Unordered map where keys are agent names and values are velocities.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::get_agent_velocities() const {
    std::unordered_map<std::string, std::vector<float>> velocities;
    for (int i = 0; i < num_agents; ++i) {
        velocities["agent_" + std::to_string(i)] = {agents[i].getVx(), agents[i].getVy(), agents[i].getVz()};
    }
    return velocities;
}

/**
 * @brief Retrieves previous positions of all agents.
 * @return Unordered map where keys are agent names and values are previous positions.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::get_previous_positions() const {
    std::unordered_map<std::string, std::vector<float>> previous_positions_map;
    for (int i = 0; i < num_agents; ++i) {
        previous_positions_map["agent_" + std::to_string(i)] = {previous_positions[i].getX(), previous_positions[i].getY(), previous_positions[i].getZ()};
    }
    return previous_positions_map;
}

/**
 * @brief Retrieves orientations of all agents.
 * @return Unordered map where keys are agent names and values are orientations.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::get_agent_orientations() const {
    std::unordered_map<std::string, std::vector<float>> orientations;
    for (int i = 0; i < num_agents; ++i) {
        // Euler angles (roll, pitch, yaw)
        orientations["agent_" + std::to_string(i)] = {0.0, 0.0, agents[i].getSteering()};
    }
    return orientations;
}

/**
 * @brief Updates the position of a vehicle based on actions.
 * @param vehicle Reference to the vehicle to update.
 * @param high_level_action The high-level action to apply.
 * @param low_level_action The low-level actions to apply.
 */
void Traffic::updatePosition(Vehicle& vehicle, int high_level_action, const std::vector<float>& low_level_action) {
    // Bound kinematics to physical constraints
    float steering = clamp(low_level_action[0], -0.610865f, 0.610865f); // Clamp steering between -35 and 35 degrees in radians
    vehicle.setSteering(steering);

    float acceleration = clamp(low_level_action[1], 0.0f, 4.5f); // Acceleration (m/s^2)
    float braking = clamp(low_level_action[2], -8.0f, 0.0f); // Braking deceleration (m/s^2)
    float net_acceleration = 0.0f;
    
    // Time step (assuming a fixed time step, adjust as necessary)
    float time_step = 0.04f; // e.g., 1.0f second or 1/25 for 25 FPS

    const float max_velocity = 60.0f; // Maximum velocity (m/s)

    // Process high-level actions
    switch (high_level_action) {
        case 0: // Keep lane
            // No changes are needed to keep the lane
            break;
        case 1: // Left lane change
            net_acceleration = 0.0;
            break;
        case 2: // Right lane change
            net_acceleration = 0.0;
            break;
        case 3: // Speed up
            net_acceleration = acceleration;
            break;
        case 4: // Slow down
            net_acceleration = braking;
            break;
    }

    // Update the velocities
    float initial_velocity_z = vehicle.getVz();
    float initial_velocity_x = vehicle.getVx();

    // Calculate the components of the net acceleration in the z and x directions
    float acceleration_z = net_acceleration * std::cos(steering);
    float acceleration_x = net_acceleration * std::sin(steering);

    float new_velocity_z = clamp(initial_velocity_z + acceleration_z * time_step, 0.0f, max_velocity);
    float new_velocity_x = clamp(initial_velocity_x + acceleration_x * time_step, 0.0f, max_velocity);

    vehicle.setVz(new_velocity_z);
    vehicle.setVx(new_velocity_x);

    // Update the position using the kinematic equations
    float delta_z = initial_velocity_z * time_step + 0.5f * acceleration_z * time_step * time_step;
    float delta_x = initial_velocity_x * time_step + 0.5f * acceleration_x * time_step * time_step;

    float new_z = vehicle.getZ() + delta_z;
    float new_x = vehicle.getX() + delta_x;

    vehicle.setZ(new_z);
    vehicle.setX(new_x);

    // Wrap around horizontally
    //if (vehicle.getZ() < 0) vehicle.setZ(vehicle.getZ() + SCREEN_WIDTH);
    //if (vehicle.getZ() >= SCREEN_WIDTH) vehicle.setZ(vehicle.getZ() - SCREEN_WIDTH);

    // Constrain vertically within the road
    //vehicle.setX(std::fmin(std::fmax(vehicle.getX(), -0.5 * (LANE_WIDTH - 0.5 * vehicle.getWidth())), 0.5 * (LANE_WIDTH - vehicle.getWidth())));
}

/**
 * @brief Retrieves nearby vehicles for a given agent.
 * @param agent_name The name of the agent.
 * @return Vector of shared pointers to nearby vehicles.
 */
 std::vector<Vehicle> Traffic::getNearbyVehicles(const std::string& agent_name) const {
    // Find the vehicle corresponding to the given agent ID
    const Vehicle* ego_vehicle = nullptr;

    for (const auto& vehicle : agents) {

        if (vehicle.getName() == agent_name) {
            ego_vehicle = &vehicle;
            break;
        }
    }

    // If the vehicle with the given agent ID is not found, return an empty vector
    if (ego_vehicle == nullptr) {
        return std::vector<Vehicle>();
    }

    // Call the PerceptionModule method to detect nearby vehicles
    return perceptionModule->detectNearbyVehicles(*ego_vehicle);
}

/**
 * @brief Gets the OpenDRIVE map associated with the simulation.
 * @return Shared pointer to the OpenDRIVE map.
 */
std::shared_ptr<odr::OpenDriveMap> Traffic::get_odr_map() const {
    return odr_map;
}

/**
 * @brief Sets the OpenDRIVE map for the simulation.
 * @param map Shared pointer to the OpenDRIVE map.
 */
void Traffic::set_odr_map(const std::shared_ptr<odr::OpenDriveMap>& map) {
    odr_map = map;
}

/**
 * @brief Checks for collisions between agents.
 * If two agents are within the vehicle width of each other, their velocities are set to zero.
 */
void Traffic::checkCollisions() {
    for (int i = 0; i < num_agents; ++i) {
        for (int j = i + 1; j < num_agents; ++j) {
            float distance = std::hypot(agents[i].getZ() - agents[j].getZ(), agents[i].getX() - agents[j].getX());
            if (distance < VEHICLE_WIDTH) {
                // Handle collision by setting velocities to zero
                /*
                agents[i].setVx(0.0f);
                agents[i].setVy(0.0f);
                agents[j].setVx(0.0f);
                agents[j].setVy(0.0f);
                */
                std::cout << "*** Collision Detected *** (distance gap " << distance << ")" << std::endl;
            }
        }
    }
}

/**
 * @brief Calculates the heading of the road at a given s coordinate.
 * @param road The road object.
 * @param s The s coordinate along the road.
 * @return The heading in radians.
 */
float Traffic::get_heading(const odr::Vec3D& vehicle_position) {
    std::cout << "++ Traffic::get_heading ++" << std::endl;

    const odr::Road* nearest_road = nullptr;
    const odr::Lane* nearest_lane = nullptr;
    float nearest_s = 0.0f;
    float nearest_t = 0.0f;
    float min_distance = std::numeric_limits<float>::max();

    std::vector<odr::Road> roads = odr_map->get_roads();
    std::cout << "NUM. ROARDS: " << roads.size() << std::endl;

    for (const auto& road : roads) {
        std::cout << "Processing Road ID: " << road.id << ", Length: " << road.length << std::endl;

        odr::Vec3D projected_pos;
        float s, t;

        try {
            project_xy_to_st(road, vehicle_position, s, t, projected_pos);
        } catch (const std::exception& e) {
            std::cout << "Exception in project_xy_to_st: " << e.what() << std::endl;
            continue;
        }

        std::cout << "Projected s: " << s << ", t: " << t << std::endl;

        // Check if s is within the road's length
        if (s < 0 || s > road.length) {
            std::cout << "Warning: s is outside road length!" << std::endl;
            continue;
        }

        float dist = distance(vehicle_position, projected_pos);
        std::cout << "dist: " << dist << std::endl;
        std::cout << "min_distance: " << min_distance<< std::endl;

        if (dist < min_distance) {
            min_distance = dist;
            nearest_road = &road;
            nearest_s = s;
            nearest_t = t;

            std::cout << "Road ID: " << road.id << ", Road Length: " << road.length << std::endl;
            std::cout << "Projected s: " << s << ", t: " << t << std::endl;

            // Check if s is within the road's length
            if (s < 0 || s > road.length) {
                std::cout << "Warning: s is outside road length!" << std::endl;
                continue;
            }

            std::cout << "Try to get lane section " << std::endl;
            try {
                odr::LaneSection lane_section = road.get_lanesection(s);
                std::cout << "Extracted lane section! " << std::endl;

                // Print lane section details
                std::cout << "Lane Section s0: " << lane_section.s0 << std::endl;
                std::cout << "Number of lanes: " << lane_section.id_to_lane.size() << std::endl;

                float accumulated_width = 0.0f;

                for (const auto& lane_pair : lane_section.id_to_lane) {
                    std::cout << "Attempt to obtain lane type!!" << std::endl;
                    const odr::Lane& lane = lane_pair.second;

                    if (lane.type == "driving" || lane.type == "exit" || lane.type == "entry") {
                        float lane_width = lane.lane_width.get(s); // Evaluate the CubicSpline at s

                        if (std::abs(t) >= accumulated_width && std::abs(t) < accumulated_width + lane_width) {
                            nearest_lane = &lane;
                            break;
                        }
                        accumulated_width += lane_width;
                    }
                    else {
                        std::cout << "* NOT A VALID LANE TYPE * " << std::endl;
                    }
                }
            } catch (const std::exception& e) {
                std::cout << "Exception when getting lane section: " << e.what() << std::endl;
            }
        }
    }

    if (!nearest_road || !nearest_lane) {
        return 0.0f;
    }

    float delta_s = 0.01f;
    odr::Vec3D pos1, pos2;

    try {
        pos1 = nearest_road->get_xyz(nearest_s, nearest_t, 0.0f);
        pos2 = nearest_road->get_xyz(nearest_s + delta_s, nearest_t, 0.0f);
    } catch (const std::exception& e) {
        std::cout << "Exception when getting XYZ coordinates: " << e.what() << std::endl;
        return 0.0f;
    }

    float dx = pos2[0] - pos1[0];
    float dy = pos2[1] - pos1[1];
    float heading = std::atan2(dy, dx);

    if (nearest_lane->id < 0) {
        heading += M_PI;
        if (heading > M_PI) heading -= 2 * M_PI;
    }
    std::cout << "Final heading: " << heading << std::endl;
    return heading;
}

/**
 * @brief Projects a 3D point (x, y, z) onto the road's s-t coordinate system.
 *
 * This method takes a 3D point in the global coordinate system and projects it onto
 * the road's local s-t coordinate system. It finds the closest point on the road to
 * the given (x, y, z) point and calculates the corresponding s (longitudinal) and
 * t (lateral) coordinates.
 *
 * @param road The road object onto which the point is being projected.
 * @param xy The 3D point in the global coordinate system to be projected.
 * @param s [out] The calculated s-coordinate (longitudinal distance along the road).
 * @param t [out] The calculated t-coordinate (lateral offset from the road's reference line).
 * @param projected_pos [out] The 3D position of the projected point on the road.
 *
 * @note This method uses a sampling approach to find the closest point on the road.
 *       The accuracy of the projection depends on the number of samples used.
 *
 * @throws std::runtime_error If there's an error in accessing road geometry or if
 *         the projection falls outside the valid road length.
 */
void Traffic::project_xy_to_st(const odr::Road& road, const odr::Vec3D& xy, float& s, float& t, odr::Vec3D& projected_pos) {
    float min_distance = std::numeric_limits<float>::max();
    float best_s = 0.0f;
    odr::Vec3D best_projection;

    float road_length = road.length;
    int num_samples = 100;

    for (int i = 0; i <= num_samples; ++i) {
        float sample_s = (i / static_cast<float>(num_samples)) * road_length;
        if (sample_s > road_length) {
            sample_s = road_length;  // Clamp to road length
        }

        odr::Vec3D sample_point;
        try {
            sample_point = road.get_xyz(sample_s, 0.0, 0.0);
        } catch (const std::exception& e) {
            std::cout << "Exception in get_xyz: " << e.what() << " at s = " << sample_s << std::endl;
            continue;
        }

        float dist = distance(xy, sample_point);

        if (dist < min_distance) {
            min_distance = dist;
            best_s = sample_s;
            best_projection = sample_point;
        }
    }

    odr::Vec3D nearest_point = road.get_xyz(best_s, 0.0, 0.0);
    float dx = xy[0] - nearest_point[0];
    float dy = xy[1] - nearest_point[1];

    odr::Vec3D next_point;
    if (best_s + 0.1 <= road_length) {
        next_point = road.get_xyz(best_s + 0.1, 0.0, 0.0);
    } else {
        next_point = road.get_xyz(road_length, 0.0, 0.0);
    }
    float road_dx = next_point[0] - nearest_point[0];
    float road_dy = next_point[1] - nearest_point[1];
    float road_heading = std::atan2(road_dy, road_dx);

    float heading = std::atan2(dy, dx);
    t = std::sqrt(dx * dx + dy * dy) * std::sin(heading - road_heading);

    s = best_s;
    projected_pos = best_projection;
}

/**
 * @brief Generates a random float within a specified range.
 *
 * @param a Lower bound of the range.
 * @param b Upper bound of the range.
 * @return Random float within the specified range.
 */
float Traffic::randFloat(float a, float b) {
    std::uniform_real_distribution<float> distribution(a, b);
    return distribution(generator);
}

/**
 * @brief Generates a random float following a normal (Gaussian) distribution.
 *
 * This function uses a normal distribution characterized by the given mean
 * and standard deviation to generate a random floating-point number.
 *
 * @param mean The mean (average) of the normal distribution.
 * @param stddev The standard deviation of the normal distribution.
 * @return Random float following the specified normal distribution.
 */
float Traffic::randNormal(float mean, float stddev) {
    std::normal_distribution<float> distribution(mean, stddev);
    return distribution(generator);
}

#include "traffic.h"
#include "perception_module.h"

#include <iostream>
#include <algorithm> // for std::max and std::min
#include <cmath> // For M_PI
#include <random>
#include <tuple>
#include <limits>
#include <iomanip>

// Include order: standard headers, then local headers
#include "Lane.h"
#include "LaneSection.h"
#include "Math.hpp"
#include "Mesh.h"
#include "OpenDriveMap.h"
#include "RoadNetworkMesh.h"
#include "Road.h"

//const float EPSILON = 1e-6f;

// Custom clamp function for C++11
template <typename T>
T clamp(T value, T min_val, T max_val) {
    return std::max(min_val, std::min(value, max_val));
}

// Normalize angle to the range [-pi, pi] relative to a road heading and lane direction
float normalizeAngleRelativeToRoad(float angle, float road_heading, int lane_id) {
    // Adjust the angle relative to the road heading
    float adjusted_angle = angle - road_heading;

    // Adjust for opposite direction lanes
    if (lane_id < 0) {
        adjusted_angle += M_PI;
        if (adjusted_angle > M_PI) adjusted_angle -= 2 * M_PI;
    }

    // Normalize to the range [-pi, pi]
    adjusted_angle = fmod(adjusted_angle + M_PI, 2 * M_PI) - M_PI;

    // Adjust back to absolute angle
    return adjusted_angle + road_heading;
}

// Helper function to calculate distance between two Vec3D points
float distance(const odr::Vec3D& a, const odr::Vec3D& b) {
    return std::sqrt(std::pow(a[0] - b[0], 2) + std::pow(a[1] - b[1], 2) + std::pow(a[2] - b[2], 2));
}

// Function to convert radians to degrees and normalize to [0, 360)
double radiansToDegrees(double radians) {
    double degrees = radians * (180.0 / M_PI);

    // Normalize to [0, 360)
    degrees = fmod(degrees, 360.0);
    if (degrees < 0) {
        degrees += 360.0;
    }

    return degrees;
}


/**
 * @brief Constructor for Traffic.
 * @param num_agents Number of agents (vehicles) in the simulation.
 */
Traffic::Traffic(const int& num_agents, const std::string& map_file, const unsigned& seed=314)
    : time_step(0.04f), num_agents(num_agents), generator(seed) {
    odr_map = std::make_shared<odr::OpenDriveMap>(map_file);
    perception_module = std::make_unique<PerceptionModule>(*this); // Initialize the pointer

    agents.resize(num_agents);
    previous_positions.resize(num_agents);

    // Get all roads from the OpenDRIVE map
    roads = odr_map->get_roads();

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
    float road_heading;
    int lane_id;
    std::tie(road_heading, lane_id) = getHeading(position);

    // Define the clamping range
    const float max_deviation = 0.610865f; // +/- 35 degrees in radians

    // Initialize agents with random positions and attributes
    for (int i = 0; i < num_agents; ++i) {
        // Generate a sample heading
        float heading = randNormal(road_heading, 1.0f);

        // To get a more accurate heading that considers the vehicle's orientation, we should incorporate the vehicle's steering angle
        // Normalize and clamp the desired heading
        heading = normalizeAngleRelativeToRoad(heading, road_heading, lane->id);

        // Clamp the heading to be within the allowable range of road heading +/- max deviation
        heading = clamp(heading, road_heading - max_deviation, road_heading + max_deviation);

        agents[i].setId(i);
        agents[i].setName("agent_" + std::to_string(i));
        agents[i].setWidth(2.0f);
        agents[i].setLength(5.0f);
        agents[i].setSensorRange(200.0f);
        agents[i].setSteering(heading);

        std::cout << "VEHICLE HEADING: " << radiansToDegrees(heading) << std::endl;
        std::cout << "ROAD HEADING: " << radiansToDegrees(road_heading) << std::endl;

        // Check if the new position is drivable
        if (isPositionDrivable(position)) {
           // Apply the new position if it is drivable
           agents[i].setX(position[0]);
           agents[i].setY(position[1]);
           agents[i].setZ(position[2]);
        } else {
            // Optionally, handle the case where the new position is not drivable
            // For example, you could move the vehicle to the nearest drivable point
            moveNearestDrivablePoint(agents[i]);
        }
        agents[i].setVx(randNormal(50.0f, 2.0f));  // Initial longitudinal speed
        agents[i].setVy(randNormal(0.0f, 0.5f));   // Initial lateral speed
        agents[i].setVz(0.0f); // Initial verticle speed
        agents[i].setLaneId(lane->id); // Set lane ID

        // Update previous position with current position
        previous_positions[i] = agents[i];
    }
}

/**
 * @brief Destructor for Traffic.
 * Cleans up memory allocated for perception module.
 * No need to delete perceptionModule explicitly; std::unique_ptr handles it
 */
Traffic::~Traffic() = default;

/**
 * @brief Performs a simulation step.
 * Updates agent positions based on high-level and low-level actions,
 * updates perceptions, and checks for collisions.
 * @param high_level_actions High-level actions for each agent.
 * @param low_level_actions Low-level actions for each agent.
 */
void Traffic::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {
    // Update positions of all agents
    for (size_t i = 0; i < agents.size(); ++i) {
        updatePosition(agents[i], high_level_actions[i], low_level_actions[i]);
    }

    // Update perceptions
    perception_module->updatePerceptions();

    // Check for collisions between agents
    //checkCollisions(); // Collision detection
}

/**
 * @brief Retrieves all agents in the simulation.
 * @return Vector of all agents.
 */
const std::vector<Vehicle>& Traffic::getAgents() const {
    return agents;
}
/**
 * @brief Retrieves an agent by its name.
 * @param name The name of the agent to retrieve.
 * @return Reference to the agent.
 * @throws std::runtime_error if the agent with the given name is not found.
 */
Vehicle& Traffic::getAgentByName(const std::string& name) {
    auto it = std::find_if(agents.begin(), agents.end(), [&name](const Vehicle& vehicle) {
        return vehicle.getName() == name;
    });

    if (it == agents.end()) {
        throw std::runtime_error("Agent with name " + name + " not found");
    }

    return *it;
}

/**
 * @brief Retrieves positions of all agents.
 * @return Unordered map where keys are agent names and values are positions.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::getAgentPositions() const {
    std::unordered_map<std::string, std::vector<float>> positions;

    for (const auto& agent : agents) {
        positions[agent.getName()] = {agent.getX(), agent.getY(), agent.getZ()};
    }

    return positions;
}

/**
 * @brief Retrieves velocities of all agents.
 * @return Unordered map where keys are agent names and values are velocities.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::getAgentVelocities() const {
    std::unordered_map<std::string, std::vector<float>> velocities;
    for (const auto& agent : agents) {
        velocities[agent.getName()] = {agent.getVx(), agent.getVy(), agent.getVz()};
    }

    return velocities;
}

/**
 * @brief Retrieves previous positions of all agents.
 * @return Unordered map where keys are agent names and values are previous positions.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::getPreviousPositions() const {
    std::unordered_map<std::string, std::vector<float>> previous_position;
    for (const auto& agent : agents) {
        previous_position[agent.getName()] = {previous_positions[agent.getId()].getX(), previous_positions[agent.getId()].getY(), previous_positions[agent.getId()].getZ()};
    }
    return previous_position;
}

/**
 * @brief Retrieves orientations of all agents.
 * @return Unordered map where keys are agent names and values are orientations.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::getAgentOrientations() const {
    std::unordered_map<std::string, std::vector<float>> orientations;
    for (const auto& agent : agents) {
        // Euler angles (roll, pitch, yaw)
        orientations[agent.getName()] = {0.0, 0.0, agent.getSteering()};
    }
    return orientations;
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
    return perception_module->detectNearbyVehicles(*ego_vehicle);
}

/**
 * @brief Gets the OpenDRIVE map associated with the simulation.
 * @return Shared pointer to the OpenDRIVE map.
 */
std::shared_ptr<odr::OpenDriveMap> Traffic::getOdrMap() const {
    return odr_map;
}

/**
 * @brief Sets the OpenDRIVE map for the simulation.
 * @param map Shared pointer to the OpenDRIVE map.
 */
void Traffic::setOdrMap(const std::shared_ptr<odr::OpenDriveMap>& map) {
    odr_map = map;
}

/**
 * @brief Calculates the heading of the road at a given s coordinate.
 * @param road The road object.
 * @param s The s coordinate along the road.
 * @return The heading in radians.
 */
std::tuple<float, int> Traffic::getHeading(const odr::Vec3D& road_position) {
    const odr::Road* nearest_road = nullptr;
    const odr::Lane* nearest_lane = nullptr;
    float nearest_s = 0.0f;
    float nearest_t = 0.0f;
    float min_distance = std::numeric_limits<float>::max();

    std::vector<odr::Road> roads = odr_map->get_roads();

    for (const auto& road : roads) {
        //std::cout << "Processing Road ID: " << road.id << ", Length: " << road.length << std::endl;

        odr::Vec3D projected_pos;
        float s = 0.0;
        float t = 0.0;

        try {
            projectXyToSt(road, road_position, s, t, projected_pos);
        } catch (const std::exception& e) {
            std::cout << "Exception in project_xy_to_st: " << e.what() << std::endl;
            continue;
        }

        //std::cout << "Projected s: " << s << ", t: " << t << std::endl;

        // Check if s is within the road's length
        // Clamp the s coordinate
        if (s < 0 || s > road.length) {
            //if (s < -EPSILON || s > road.length + EPSILON) {
            continue;
        }

        float dist = distance(road_position, projected_pos);

        if (dist < min_distance) {
            min_distance = dist;
            nearest_road = &road;
            nearest_s = s;
            nearest_t = t;

            // Check if s is within the road's length
            // Clamp the s coordinate
            if (s < 0 || s > road.length) {
                //if (s < -EPSILON || s > road.length + EPSILON) {
                continue;
            }

            // Try to get lane section
            try {
                // Extract lane section
                odr::LaneSection lane_section = road.get_lanesection(s);

                // Print lane section details
                //std::cout << "Lane Section s0: " << lane_section.s0 << std::endl;
                //std::cout << "Number of lanes: " << lane_section.id_to_lane.size() << std::endl;

                float accumulated_width = 0.0f;

                for (const auto& lane_pair : lane_section.id_to_lane) {
                    const odr::Lane& lane = lane_pair.second;

                    if (lane.type == "driving" || lane.type == "exit" || lane.type == "entry") {
                        float lane_width = lane.lane_width.get(s); // Evaluate the CubicSpline at s

                        if (std::abs(t) >= accumulated_width && std::abs(t) < accumulated_width + lane_width) {
                            nearest_lane = &lane;
                            break;
                        }
                        accumulated_width += lane_width;
                    }
                    /*
                    else {
                        std::cout << "* NOT A VALID LANE TYPE * " << std::endl;
                    }
                    */
                }
            } catch (const std::exception& e) {
                std::cout << "Exception when getting lane section: " << e.what() << std::endl;
            }
        }
    }

    if (!nearest_road || !nearest_lane) {
        return std::make_tuple(0.0f, nearest_lane->id);
    }

    float delta_s = 0.01f;
    odr::Vec3D pos1, pos2;

    try {
        pos1 = nearest_road->get_xyz(nearest_s, nearest_t, 0.0f);
        pos2 = nearest_road->get_xyz(nearest_s + delta_s, nearest_t, 0.0f);
    } catch (const std::exception& e) {
        std::cout << "Exception when getting XYZ coordinates: " << e.what() << std::endl;
        return std::make_tuple(0.0f, nearest_lane->id);
    }

    float dx = pos2[0] - pos1[0];
    float dy = pos2[1] - pos1[1];
    float heading = std::atan2(dy, dx);

    // Adjusts the heading by π (180 degrees) if the lane ID is negative, which typically indicates a lane on the left side of the road.
    // The rationale behind this adjustment is to reverse the direction of the heading since lanes with negative IDs are usually opposite
    // to the direction of the road's reference line.
    if (nearest_lane->id < 0) {
        heading += M_PI;
        if (heading > M_PI) heading -= 2 * M_PI;
    }

    return std::make_tuple(heading, nearest_lane->id);
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
void Traffic::projectXyToSt(const odr::Road& road, const odr::Vec3D& xy, float& s, float& t, odr::Vec3D& projected_pos) {
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
 * @brief Checks if a given position is drivable.
 * @param position The 3D position to check.
 * @return True if the position is drivable, false otherwise.
 */
bool Traffic::isPositionDrivable(const odr::Vec3D& position) {
    const odr::Road* nearest_road = nullptr;
    const odr::Lane* nearest_lane = nullptr;
    float min_distance = std::numeric_limits<float>::max();

    std::vector<odr::Road> roads = odr_map->get_roads();

    for (const auto& road : roads) {
        odr::Vec3D projected_pos;
        float s = 0.0;
        float t = 0.0;

        try {
            projectXyToSt(road, position, s, t, projected_pos);
        } catch (const std::exception& e) {
            continue;
        }

        float dist = distance(position, projected_pos);

        if (dist < min_distance) {
            min_distance = dist;
            nearest_road = &road;

            // Clamp the s coordinate
            if (s < 0 || s > road.length) {
                continue;
            }

            try {
                odr::LaneSection lane_section = road.get_lanesection(s);
                float accumulated_width = 0.0f;
                float total_width = 0.0f;

                for (const auto& lane_pair : lane_section.id_to_lane) {
                    const odr::Lane& lane = lane_pair.second;
                    float lane_width = lane.lane_width.get(s);
                    total_width += lane_width;

                    if (lane.type == "driving" || lane.type == "exit" || lane.type == "entry") {

                        if (std::abs(t) >= accumulated_width && std::abs(t) < accumulated_width + lane_width) {
                            nearest_lane = &lane;
                            break;
                        }
                    }
                    accumulated_width += lane_width;
                }
                // Check if the position is within the road's outer boundaries
                if (std::abs(t) > total_width / 2) {
                    nearest_lane = nullptr;
                }
            } catch (const std::exception& e) {
                continue;
            }
        }
    }

    return (nearest_road != nullptr && nearest_lane != nullptr);
}

/**
 * @brief Moves the vehicle to the nearest drivable point.
 * @param vehicle Reference to the vehicle to be moved.
 */
void Traffic::moveNearestDrivablePoint(Vehicle& vehicle) {
    odr::Vec3D position{vehicle.getX(), vehicle.getY(), vehicle.getZ()};
    std::pair<const odr::Road*, odr::Vec3D> result = getNearestRoadPoint(position);

    const odr::Road* nearest_road = result.first;
    odr::Vec3D nearest_point = result.second;

    // Project the vehicle onto the nearest road
    if (nearest_road != nullptr) {
        vehicle.setX(nearest_point[0]);
        vehicle.setY(nearest_point[1]);
        vehicle.setZ(nearest_point[2]);

        std::cout << "Vehicle moved to nearest road point: ("
                  << vehicle.getX() << ", " << vehicle.getY() << ", " << vehicle.getZ() << ")" << std::endl;
    } else {
        std::cout << "Warning: No road found" << std::endl;
    }
}

/*
void Traffic::moveNearestDrivablePoint(Vehicle& vehicle) {
    const float MAX_SEARCH_RADIUS = 50.0f; // Maximum search radius in meters (default: 10.0f)
    const float SEARCH_STEP = 0.25f; // Step size for the search in meters (default: 0.5f)

    odr::Vec3D current_pos{vehicle.getX(), vehicle.getY(), vehicle.getZ()};
    odr::Vec3D nearest_drivable_pos = current_pos;
    float min_distance = std::numeric_limits<float>::max();

    // Search in a circular pattern around the current position
    for (float radius = 0.5f; radius <= MAX_SEARCH_RADIUS; radius += SEARCH_STEP) {
        for (float angle = 0; angle < 2 * M_PI; angle += M_PI / 8) { // /8
            float x = current_pos[0] + radius * std::cos(angle);
            float y = current_pos[1] + radius * std::sin(angle);
            odr::Vec3D test_pos{x, y, current_pos[2]};

            if (isPositionDrivable(test_pos)) {
                float dist = distance(current_pos, test_pos);

                if (dist < min_distance) {
                    min_distance = dist;
                    nearest_drivable_pos = test_pos;
                }
            }
        }

        // If we've found a drivable position, break the search
        if (min_distance < std::numeric_limits<float>::max()) {
            break;
        }
    }

    // Move the vehicle to the nearest drivable position
    if (min_distance < std::numeric_limits<float>::max()) {
        vehicle.setX(nearest_drivable_pos[0]);
        vehicle.setY(nearest_drivable_pos[1]);
        vehicle.setZ(nearest_drivable_pos[2]);

        std::cout << "Vehicle moved to nearest drivable point: ("
                  << nearest_drivable_pos[0] << ", " << nearest_drivable_pos[1] << ", " << nearest_drivable_pos[2] << ")" << std::endl;

    } else {
        std::cout << "Warning: No drivable point found within search radius" << std::endl;
    }
}
*/

/**
 * @brief Finds the nearest road point for a given position.
 * @param x The x-coordinate of the position.
 * @param y The y-coordinate of the position.
 * @return A pair containing a pointer to the nearest road and the nearest point on that road.
 */
std::pair<const odr::Road*, odr::Vec3D> Traffic::getNearestRoadPoint(const odr::Vec3D& position) const {
    float min_distance = std::numeric_limits<float>::max();
    odr::Vec3D nearest_point;
    const odr::Road* nearest_road = nullptr;

    // Calculate the shortest distance from a point (x, y) to any point on the road
    for (const auto& road : roads) {
        for (float s = 0; s < road.length; s += 0.5f) {  // Adjust step size as needed
            odr::Vec3D road_point = road.get_xyz(s, 0.0f, 0.0f);
            float distance = std::hypot(position[0] - road_point[0], position[1] - road_point[1]);

            if (distance < min_distance) {
                min_distance = distance;
                nearest_point = road_point;
                nearest_road = &road;
            }
        }
    }

    return {nearest_road, nearest_point};
}

/**
 * @brief Computes the tangent vector to the road at a specified longitudinal position.
 *
 * This method calculates the tangent vector to the road at a given longitudinal coordinate `s`
 * by numerically differentiating the road's position along the s-direction. The tangent vector
 * is then normalized to ensure it has a unit length.
 *
 * @param road The `odr::Road` object representing the road.
 * @param s The longitudinal distance along the road at which the tangent is calculated.
 * @return The normalized tangent vector as an `odr::Vec3D` object, representing the direction
 *         of the road at the given position.
 *
 * @note The tangent vector is computed using a small delta for numerical differentiation. The
 *       tangent is returned as a unit vector in the direction of the road's path.
 */
odr::Vec3D Traffic::getTangent(const odr::Road& road, double s) const {
    double delta = 0.1; // Small delta for numerical differentiation
    odr::Vec3D p1 = road.get_xyz(s, 0.0, 0.0);
    odr::Vec3D p2 = road.get_xyz(s + delta, 0.0, 0.0);

    odr::Vec3D tangent = {
        p2[0] - p1[0],
        p2[1] - p1[1],
        p2[2] - p1[2]
    };

    double length = std::sqrt(tangent[0]*tangent[0] + tangent[1]*tangent[1] + tangent[2]*tangent[2]);
    tangent[0] /= length;
    tangent[1] /= length;
    tangent[2] /= length;

    return tangent;
}

/**
 * @brief Computes the normal vector to the road at a specified longitudinal position.
 *
 * This method calculates the normal vector to the road at a given longitudinal coordinate `s`
 * by rotating the tangent vector 90 degrees clockwise in the xy-plane. The resulting vector
 * is perpendicular to the road's path at the given position and lies in the xy-plane.
 *
 * @param road The `odr::Road` object representing the road.
 * @param s The longitudinal distance along the road at which the normal is calculated.
 * @return The normal vector as an `odr::Vec3D` object, perpendicular to the tangent vector
 *         of the road at the given position.
 *
 * @note The normal vector is derived from the tangent vector by rotating it 90 degrees
 *       clockwise in the xy-plane, assuming the road's elevation is constant.
 */
odr::Vec3D Traffic::getNormal(const odr::Road& road, double s) const {
    odr::Vec3D tangent = getTangent(road, s);

    // Rotate tangent 90 degrees clockwise on the xy-plane
    return {-tangent[1], tangent[0], 0.0};
}

/**
 * @brief Projects a global position onto the nearest point along the road's path.
 *
 * This method finds the closest longitudinal position `s` on the road that projects the given
 * global position onto the road. It uses a binary search approach to iteratively narrow down
 * the range of `s` values until it finds the closest point on the road.
 *
 * @param road A pointer to the `odr::Road` object representing the road.
 * @param position The global coordinates (x, y, z) of the point to be projected onto the road.
 * @return The longitudinal position `s` along the road that is closest to the given position.
 *
 * @note The precision of the result can be adjusted by modifying the termination condition of
 *       the while loop. The method performs a binary search over the road's length to find the
 *       optimal position.
 */
double Traffic::projectPointOntoRoad(const odr::Road* road, const odr::Vec3D& position) {
    double s_start = 0;
    double s_end = road->length;

    while (s_end - s_start > 0.01) { // Adjust precision as needed
        double s_mid = (s_start + s_end) / 2;
        odr::Vec3D point = road->get_xyz(s_mid, 0, 0);
        odr::Vec3D tangent = getTangent(*road, s_mid);

        double dx = point[0] - position[0];
        double dy = point[1] - position[1];

        if (dx * tangent[0] + dy * tangent[1] > 0) {
            s_end = s_mid;
        } else {
            s_start = s_mid;
        }
    }

    return (s_start + s_end) / 2;
}

/**
 * @brief Calculates the lateral offset of a given position from a road's reference line.
 *
 * This method computes the lateral offset of a point (x, y) from the reference line of a road at
 * a specified longitudinal distance `s`. The lateral offset is the perpendicular distance from the
 * point to the reference line of the road, taking into account the road's normal vector at that point.
 *
 * @param road A pointer to the `odr::Road` object representing the road.
 * @param s The longitudinal distance along the road where the lateral offset is calculated.
 * @param position The position (x, y, z) in global coordinates from which the lateral offset is measured.
 * @return The lateral offset (t) from the road's reference line in the direction perpendicular to the road.
 *
 * @note The lateral offset is computed by projecting the vector from the road's reference point to the
 *       given position onto the normal vector of the road's reference line at the given `s` coordinate.
 *       The `getNormal` function is used to obtain the normal vector for this calculation.
 */
double Traffic::calculateLateralOffset(const odr::Road* road, double s, const odr::Vec3D& position) {
    odr::Vec3D referencePoint = road->get_xyz(s, 0, 0);
    odr::Vec3D normal = getNormal(*road, s);

    double dx = position[0] - referencePoint[0];
    double dy = position[1] - referencePoint[1];

    return dx * normal[0] + dy * normal[1];
}


/**
 * @brief Converts global coordinates (x, y) to Frenet coordinates (s, t) on the nearest road.
 *
 * This method projects the given global (x, y) coordinates onto the nearest road and
 * computes the corresponding Frenet coordinates (s, t). The Frenet coordinate `s` represents
 * the longitudinal distance along the road, while `t` represents the lateral offset from the road's
 * reference line. The method also identifies the nearest lane section and lane for potential use.
 *
 * @param x The x-coordinate in global coordinates.
 * @param y The y-coordinate in global coordinates.
 * @return A `std::pair<double, double>` where the first element is the longitudinal distance `s`
 *         and the second element is the lateral offset `t` in Frenet coordinates.
 *
 * @throws std::runtime_error If no road is found for the given coordinates.
 *
 * @note The method utilizes internal functions such as `getNearestRoadPoint`, `projectPointOntoRoad`,
 *       and `calculateLateralOffset` to perform the conversion. It also retrieves lane information,
 *       but this information is not used in the conversion and can be ignored if not needed.
 */
std::pair<double, double> Traffic::convertToFrenetCoordinates(double x, double y) {
    odr::Vec3D position{x, y, 0};

    auto [nearest_road, nearest_point] = getNearestRoadPoint(position);

    if (!nearest_road) {
        throw std::runtime_error("No road found for the given coordinates");
    }

    double s = projectPointOntoRoad(nearest_road, position);
    double t = calculateLateralOffset(nearest_road, s, position);

    // Get the lane section and lane
    odr::LaneSection laneSection = nearest_road->get_lanesection(s);
    odr::Lane lane = laneSection.get_lane(s, t);

    // We can use the lane information if needed
    //int laneId = laneSection.get_lane_id(s, t);

    return std::make_pair(s, t);
}


/**
 * @brief Updates the position of a vehicle based on actions.
 * @param vehicle Reference to the vehicle to update.
 * @param high_level_action The high-level action to apply.
 * @param low_level_action The low-level actions to apply.
 */
void Traffic::updatePosition(Vehicle& vehicle, int high_level_action, const std::vector<float>& low_level_action) {
    // Extract road heading
    odr::Vec3D road_position{vehicle.getX(), vehicle.getY(), vehicle.getZ()};
    //float road_heading = getHeading(road_position);
    float road_heading;
    int lane_id;
    std::tie(road_heading, lane_id) = getHeading(road_position);

    // Bound kinematics to physical constraints
    float heading = low_level_action[0];

    // Normalize and clamp the desired heading
    heading = normalizeAngleRelativeToRoad(heading, road_heading, lane_id);

    // Clamp the heading to be within the allowable range of road heading +/- max deviation
    const float max_deviation = 0.610865f;  // +/- 35 degrees in radians
    heading = clamp(heading, road_heading - max_deviation, road_heading + max_deviation);

    vehicle.setSteering(heading);
    vehicle.setLaneId(lane_id);

    std::cout << "DRIVE HEADING: " << radiansToDegrees(vehicle.getSteering()) << std::endl;
    std::cout << "DRIVE HEADING: " << radiansToDegrees(road_heading) << std::endl;

    float acceleration = clamp(low_level_action[1], 0.0f, 4.5f); // Acceleration (m/s^2)
    float braking = clamp(low_level_action[2], -8.0f, 0.0f); // Braking deceleration (m/s^2)
    float net_acceleration = 0.0f;

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
    float initial_velocity_x = vehicle.getVx();
    float initial_velocity_y = vehicle.getVy();

    // Calculate the components of the net acceleration in the x and y directions
    float acceleration_x = net_acceleration * std::cos(heading);
    float acceleration_y = net_acceleration * std::sin(heading);

    float new_velocity_x = clamp(initial_velocity_x + acceleration_x * time_step, 0.0f, max_velocity);
    float new_velocity_y = clamp(initial_velocity_y + acceleration_y * time_step, 0.0f, max_velocity);

    vehicle.setVx(new_velocity_x);
    vehicle.setVy(new_velocity_y);

    // Update the position using the kinematic equations
    float delta_x = initial_velocity_x * time_step + 0.5f * acceleration_x * time_step * time_step;
    float delta_y = initial_velocity_y * time_step + 0.5f * acceleration_y * time_step * time_step;

    float new_x = vehicle.getX() + delta_x;
    float new_y = vehicle.getY() + delta_y;

    // Check if the new position is drivable
    odr::Vec3D new_position{new_x, new_y, 0.0f};

    if (isPositionDrivable(new_position)) {
        // Apply the new position if it is drivable
        vehicle.setX(new_position[0]);
        vehicle.setY(new_position[1]);
        vehicle.setZ(new_position[2]);
    } else {
        // Optionally, handle the case where the new position is not drivable
        // For example, you could move the vehicle to the nearest drivable point
        moveNearestDrivablePoint(vehicle);
    }
}

/**
 * @brief Checks for collisions between agents.
 * If two agents are within the vehicle width of each other, their velocities are set to zero.
 */
void Traffic::checkCollisions() {
    for (int i = 0; i < num_agents; ++i) {
        for (int j = i + 1; j < num_agents; ++j) {
            float distance = std::hypot(agents[i].getX() - agents[j].getX(), agents[i].getY() - agents[j].getY());
            if (distance < agents[i].getWidth()) {
                // Handle collision by setting velocities to zero
                /*
                agents[i].setVx(0.0f);
                agents[i].setVy(0.0f);
                agents[j].setVz(0.0f);
                */
                std::cout << "*** Collision Detected *** (distance gap " << distance << ")" << std::endl;
            }
        }
    }
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

#include "traffic.h"

#include <iostream>
#include <algorithm> // for std::max and std::min
#include <cmath>
#include <random>
#include <OpenDriveMap.h>


// Custom clamp function for C++11
template <typename T>
T clamp(T value, T min_val, T max_val) {
    return std::max(min_val, std::min(value, max_val));
}


/**
 * @brief Constructor for Traffic.
 * @param num_agents Number of agents (vehicles) in the simulation.
 */
Traffic::Traffic(const int& num_agents, const unsigned& seed) : max_speed_(60.0f), num_agents(num_agents), seed(seed) {
    std::cout << "Traffic simulation initialized with seed: " << seed << std::endl;

    // Initialize vehicle and perception related data
    generator.seed(seed); // Initialize the generator with the seed

    agents.resize(num_agents);
    previous_positions.resize(num_agents);
    vehicle_models.resize(num_agents);

    // DON'T initialize agents here - wait for map assignment
    // sampleAndInitializeAgents() will be called from Unity after map is set

    max_speed_ = 60.0f; // Use default max speed instead of agents[0]
}

/**
 * @brief Destructor for Traffic.
 * Cleans up memory allocated for perception module.
 */
Traffic::~Traffic() {
    // No need to delete perceptionModule explicitly; std::unique_ptr handles it
}

/**
 * @brief Sets the OpenDRIVE map for the simulation.
 * @param map Pointer to the OpenDriveMap object.
 */
void Traffic::setMap(odr::OpenDriveMap* map) {
    this->map_ = map;
    if (this->map_) {
        // Count roads for debug info
        int roadCount = this->map_->get_roads().size();
        std::cout << "SUCCESS: Map assigned to Traffic simulation! Found " << roadCount << " roads. Vehicles will spawn on road geometry." << std::endl;
    } else {
        std::cout << "WARNING: Null map assigned to Traffic simulation." << std::endl;
    }
}

/**
 * @brief Samples and initializes agents with random positions and attributes.
 */
/**
 * @brief Samples and initializes agents with positions on the road if map is available.
 */
void Traffic::sampleAndInitializeAgents() {
    // Shared initialization lambda
    auto initAgentBasic = [&](int i) {
        agents[i].setId(i);
        agents[i].setName("agent_" + std::to_string(i));
        agents[i].setWidth(2.0f);
        agents[i].setLength(5.0f);
        agents[i].setSensorRange(200.0f);
    };

    if (!map_) {
        std::cout << "ERROR: No map available! Map was not assigned to Traffic simulation. Spawning agents randomly off-road." << std::endl;
        for (int i = 0; i < num_agents; ++i) {
            initAgentBasic(i);
            float delta = agents[i].getWidth();
            agents[i].setX(randFloat(-5.0f * delta, 5.0f * delta));
            agents[i].setY(0.4f);
            agents[i].setZ(0.0f);  // Place at road level instead of 500+ units away
            agents[i].setVx(0.0f); agents[i].setVy(0.0f); agents[i].setVz(randNormal(25.0f, 2.0f));
            agents[i].setSteering(clamp(randNormal(0.0f, 1.0f), -static_cast<float>(M_PI)/4.0f, static_cast<float>(M_PI)/4.0f));
            previous_positions[i] = agents[i];
        }
        return;
    }

    std::cout << "Map available. Spawning agents on valid roads..." << std::endl;

    std::vector<const odr::Road*> roads;
    for (const auto& road : map_->get_roads()) {
        roads.push_back(&road);
    }

    if (roads.empty()) {
        std::cerr << "Map has no roads! Fallback to random." << std::endl;
        return;
    }

    std::uniform_int_distribution<> road_dist(0, roads.size() - 1);
    
    for (int i = 0; i < num_agents; ++i) {
        initAgentBasic(i);

        bool placed = false;
        int attempts = 0;
        const int MAX_ATTEMPTS = 100;

        while (attempts++ < MAX_ATTEMPTS && !placed) {
            const odr::Road* road = roads[road_dist(generator)];
            double s = randFloat(0.0f, road->length);
            
            odr::LaneSection lanesection = road->get_lanesection(s);
            
            std::vector<int> driving_lane_ids;
            for (const auto& lane : lanesection.get_lanes()) {
                if (lane.type == "driving") {
                    driving_lane_ids.push_back(lane.id);
                }
            }

            if (driving_lane_ids.empty()) continue;

            std::uniform_int_distribution<> lane_idx_dist(0, driving_lane_ids.size() - 1);
            int lane_id = driving_lane_ids[lane_idx_dist(generator)];

            // Get actual lane width from OpenDRIVE data
            const odr::Lane* selected_lane = nullptr;
            for (const auto& lane : lanesection.get_lanes()) {
                if (lane.id == lane_id) {
                    selected_lane = &lane;
                    break;
                }
            }

            double lane_width = 3.5; // Default fallback width
            if (selected_lane && !selected_lane->lane_width.empty()) {
                // Get lane width at position s (evaluate at the current s position)
                lane_width = selected_lane->lane_width.get(s, 3.5);
                if (lane_width <= 0) lane_width = 3.5;
            }

            // Calculate lateral position t to center vehicle in the lane
            // In OpenDRIVE, lanes are numbered: ...-3, -2, -1, 0, +1, +2, +3...
            // Lane 0 is the reference line, positive lanes are to the left, negative to the right
            double t = 0.0;
            if (lane_id > 0) {
                // Left lanes: lane center is at (lane_id - 0.5) * lane_width
                t = (lane_id - 0.5) * lane_width;
            } else if (lane_id < 0) {
                // Right lanes: lane center is at -(abs(lane_id) - 0.5) * lane_width
                t = -(std::abs(lane_id) - 0.5) * lane_width;
            }

            odr::Vec3D heading_vec;
            odr::Vec3D pos;
            try {
                pos = road->get_xyz(s, t, 0.0, &heading_vec);
            } catch (...) {
                std::cerr << "Error getting position for agent " << i << " at s=" << s << ", t=" << t << std::endl;
                continue; // Try next attempt
            }

            // Debug logging
            std::cout << "Agent " << i << " OpenDRIVE coords: X=" << pos[0]
                      << " Y=" << pos[1] << " Z=" << pos[2] << std::endl;

            // Transform from OpenDRIVE coordinates to Unity coordinates
            // This transformation MUST match MapAccessorRenderer.cs exactly
            // OpenDRIVE: X=east, Y=north, Z=up
            // Unity: X=right, Y=up, Z=forward
            // MapAccessorRenderer: X->X, Z->Y+offset, -Y->Z
            float unity_x = pos[0];              // X (east) -> X (right)
            float unity_y = pos[2] + 0.5f;       // Z (up) -> Y (up) + offset
            float unity_z = -pos[1];             // -Y (north) -> Z (forward)

            std::cout << "Agent " << i << " Unity coords: X=" << unity_x
                      << " Y=" << unity_y << " Z=" << unity_z << std::endl;

            agents[i].setX(unity_x);
            agents[i].setY(unity_y);
            agents[i].setZ(unity_z);

            // Transform heading from OpenDRIVE to Unity coordinate system
            // OpenDRIVE: X=east, Y=north, Z=up
            // Unity: X=right, Y=up, Z=forward
            // Transformation: OpenDRIVE X->Unity X, OpenDRIVE Y->Unity -Z
            double heading = std::atan2(-heading_vec[1], heading_vec[0]);

            // In OpenDRIVE, lane direction is determined by lane properties, not just sign
            // For proper lane direction, we should check the lane's travel direction
            // For now, apply the standard convention: negative lane IDs go opposite to reference line
            if (lane_id < 0) {
                heading += M_PI;
            }

            // Normalize heading to [-π, π] range
            while (heading > M_PI) heading -= 2.0 * M_PI;
            while (heading < -M_PI) heading += 2.0 * M_PI;

            agents[i].setYaw(heading);
            agents[i].setSteering(0.0f);
            agents[i].setVz(10.0f);
            agents[i].setVx(0.0f); agents[i].setVy(0.0f);

            previous_positions[i] = agents[i];
            placed = true;
        }

        if (!placed) {
             std::cerr << "Failed to place agent " << i << ". Fallback." << std::endl;
             agents[i].setX(0); agents[i].setY(5); agents[i].setZ(0); 
        }
    }
}

/**
 * @brief Performs a simulation step.
 * Updates agent positions based on high-level and low-level actions,
 * @param high_level_actions High-level actions for each agent.
 * @param low_level_actions Low-level actions for each agent.
 */
void Traffic::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {
    // Update positions of all agents
    for (auto& agent : agents) {
        applyActions(agent, high_level_actions[agent.getId()], low_level_actions[agent.getId()]);
    }
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
const Vehicle& Traffic::get_agent_by_name(const std::string& name) const {
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
    positions.reserve(num_agents); // Pre-allocate capacity
    
    for (int i = 0; i < num_agents; ++i) {
        const std::string agent_key = "agent_" + std::to_string(i);
        positions[agent_key] = {agents[i].getX(), agents[i].getY(), agents[i].getZ()};
    }
    return positions;
}

/**
 * @brief Retrieves velocities of all agents.
 * @return Unordered map where keys are agent names and values are velocities.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::get_agent_velocities() const {
    std::unordered_map<std::string, std::vector<float>> velocities;
    velocities.reserve(num_agents); // Pre-allocate capacity
    
    for (int i = 0; i < num_agents; ++i) {
        const std::string agent_key = "agent_" + std::to_string(i);
        velocities[agent_key] = {agents[i].getVx(), agents[i].getVy(), agents[i].getVz()};
    }
    return velocities;
}

/**
 * @brief Retrieves previous positions of all agents.
 * @return Unordered map where keys are agent names and values are previous positions.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::get_previous_positions() const {
    std::unordered_map<std::string, std::vector<float>> previous_positions_map;
    previous_positions_map.reserve(num_agents); // Pre-allocate capacity
    
    for (int i = 0; i < num_agents; ++i) {
        const std::string agent_key = "agent_" + std::to_string(i);
        previous_positions_map[agent_key] = {previous_positions[i].getX(), previous_positions[i].getY(), previous_positions[i].getZ()};
    }
    return previous_positions_map;
}

/**
 * @brief Retrieves orientations of all agents.
 * @return Unordered map where keys are agent names and values are orientations.
 */
std::unordered_map<std::string, std::vector<float>> Traffic::get_agent_orientations() const {
    std::unordered_map<std::string, std::vector<float>> orientations;
    orientations.reserve(num_agents); // Pre-allocate capacity
    
    for (int i = 0; i < num_agents; ++i) {
        // Euler angles (roll, pitch, yaw)
        float roll = 0.0;    // Replace with actual roll if available
        float pitch = 0.0;   // Replace with actual pitch if available
        float yaw = agents[i].getYaw(); // Not assuming yaw represents steering
        const std::string agent_key = "agent_" + std::to_string(i);
        orientations[agent_key] = {roll, pitch, yaw};
    }

    return orientations;
}

/**
 * @brief Updates the position of a vehicle based on actions.
 * @param vehicle Reference to the vehicle to update.
 * @param high_level_action The high-level action to apply.
 * @param low_level_action The low-level actions to apply.
 */
void Traffic::applyActions(Vehicle& vehicle, int high_level_action, const std::vector<float>& low_level_action) {
    int vehicle_id = vehicle.getId();

    // Store previous position
    previous_positions[vehicle_id] = vehicle;

    // Get current bicycle model state
    Vehicle& next_state = agents[vehicle_id];

    // Process steering and acceleration inputs
    float desired_steering = low_level_action[0];
    float acceleration = low_level_action[1];
    float braking = low_level_action[2];

    // === STEERING MAGNITUDE AND RATE LIMITING ===
    // Real vehicles have physical steering angle limits and realistic steering rates

    // 1. Clamp desired steering to realistic physical limits
    // Typical passenger car: ±35-45 degrees (±0.61-0.79 radians)
    constexpr float MAX_STEERING_ANGLE = 0.698f;  // 40 degrees in radians
    desired_steering = clamp(desired_steering, -MAX_STEERING_ANGLE, MAX_STEERING_ANGLE);

    // 2. Apply steering rate limiting for realistic responsiveness
    // Reduced from 60 deg/s to 30 deg/s for more realistic response
    constexpr float MAX_STEERING_RATE = 0.5236f;  // 30 deg/s in rad/s

    float current_steering = vehicle.getSteering();
    float steering_change = desired_steering - current_steering;
    float max_change = MAX_STEERING_RATE * time_step;

    // Clamp steering change to maximum rate
    if (std::abs(steering_change) > max_change) {
        steering_change = (steering_change > 0.0f) ? max_change : -max_change;
    }

    float actual_steering = current_steering + steering_change;
    // === END STEERING MAGNITUDE AND RATE LIMITING ===

    float net_acceleration = 0.0f;

    // Process high-level actions
    switch (high_level_action) {
        case 3: // Speed up
            net_acceleration = acceleration;
            break;
        case 4: // Slow down
            net_acceleration = braking;
            break;
        default:
            net_acceleration = 0.0f;
    }

    // Prevent further acceleration if the vehicle's absolute speed along the Z-axis exceeds the maximum speed.
    if (std::abs(vehicle.getZ()) > vehicle.getVehicleMaxSpeed()) {
        net_acceleration = 0.0f;
    }

    // Update vehicle state using enhanced dynamic bicycle model
    next_state = vehicle_models[vehicle_id].updateDynamicState(
        vehicle, // Current state
        actual_steering,  // Use rate-limited steering instead of desired_steering
        net_acceleration,
        time_step
    );

    // Update vehicle properties based on bicycle model state
    // Unity coordinate system: X=lateral, Y=vertical, Z=longitudinal
    vehicle.setX(next_state.getX());  // X = lateral position
    vehicle.setZ(next_state.getZ());  // Z = longitudinal/forward position

    vehicle.setYaw(next_state.getYaw());
    vehicle.setSteering(actual_steering);  // Store rate-limited steering value

    vehicle.setVx(next_state.getVx()); // Vx = lateral speed
    vehicle.setVz(next_state.getVz()); // Vz = longitudinal speed
}

/**
* @brief Getter for the time step.
* @return Current time step value.
*/
float Traffic::getTimeStep() const {
    return time_step;
}

/**
* @brief Setter for the time step.
* @param new_time_step New time step value. Must be greater than 0.
*/
void Traffic::setTimeStep(float new_time_step) {
    if (new_time_step > 0) { // Ensure valid positive time step
        time_step = new_time_step;
    }
}

/**
* @brief Getter for the maximum velocity.
* @return Current maximum velocity value.
*/
float Traffic::getMaxVehicleSpeed() const {
    return max_speed_;
}

/**
 * @brief Setter for the maximum velocity.
 * @param new_max_velocity New maximum velocity value. Must be positive.
*/
void Traffic::setMaxVehicleSpeed(float max_speed) {
    if (max_speed > 0) { // Ensure velocity is positive
        max_speed_ = max_speed;
    }
}

/**
* @brief Getter for the random seed.
* @return The seed value used for random generation.
*/
unsigned int Traffic::getSeed() const {
    return seed;
}

/**
 * @brief Generates a random float within a specified range.
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

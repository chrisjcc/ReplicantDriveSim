#include "traffic.h"
#include "perception_module.h"

#include <iostream>
#include <algorithm> // for std::max and std::min
#include <cmath>
#include <random>

const int SCREEN_WIDTH = 3000;
const int VEHICLE_WIDTH = 2;
const int LANE_WIDTH = 5;

// Custom clamp function for C++11
template <typename T>
T clamp(T value, T min_val, T max_val) {
    return std::max(min_val, std::min(value, max_val));
}


/**
 * @brief Constructor for Traffic.
 * @param num_agents Number of agents (vehicles) in the simulation.
 */
Traffic::Traffic(const int& num_agents, const unsigned& seed) : num_agents(num_agents), seed(seed) {
    std::cout << "Traffic simulation initialized with seed: " << seed << std::endl;

    // Initialize vehicle and perception related data
    generator.seed(seed); // Initialize the generator with the seed

    agents.resize(num_agents);
    previous_positions.resize(num_agents);

    perceptionModule = std::make_unique<PerceptionModule>(*this); // Initialize the pointer

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
        agents[i].setSteering(clamp(randNormal(0.0f, 1.0f), -0.610865f, 0.610865f)); // +/- 35 degrees (in rad)

        // Initialize previous positions with current positions
        previous_positions[i] = agents[i];
    }
}

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
        float roll = 0.0;    // Replace with actual roll if available
        float pitch = 0.0;   // Replace with actual pitch if available
        float yaw = agents[i].getSteering(); // Assuming steering represents yaw
        orientations["agent_" + std::to_string(i)] = {roll, pitch, yaw};
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
    if (vehicle.getZ() < 0) vehicle.setZ(vehicle.getZ() + SCREEN_WIDTH);
    if (vehicle.getZ() >= SCREEN_WIDTH) vehicle.setZ(vehicle.getZ() - SCREEN_WIDTH);

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

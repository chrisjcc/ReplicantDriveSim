#include "traffic.h"

#include <iostream>
#include <algorithm> // for std::max and std::min
#include <cmath>
#include <random>

//const int SCREEN_WIDTH = 3000;
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
    vehicle_models.resize(num_agents);
    vehicle_states.resize(num_agents);

    // Initialize agents with random positions and attributes
    for (int i = 0; i < num_agents; ++i) {
        agents[i].setId(i);
        agents[i].setName("agent_" + std::to_string(i));
        agents[i].setWidth(2.0f);
        agents[i].setLength(5.0f);
        agents[i].setSensorRange(200.0f);

        float delta = (LANE_WIDTH - agents[i].getWidth());

        agents[i].setX(randFloat(-0.5f * delta, 0.5f * delta));
        agents[i].setY(0.0f);
        agents[i].setZ(randFloat(0.0f, 4.0f * agents[i].getLength()));
        agents[i].setVx(0.0f);  // Initial lateral speed
        agents[i].setVy(0.0f);  // Initial verticle speed
        agents[i].setVz(randNormal(50.0f, 2.0f)); // Initial longitudinal speed
        agents[i].setSteering(clamp(randNormal(0.0f, 1.0f), -0.610865f, 0.610865f)); // +/- 35 degrees (in rad)

        // Initialize previous positions with current positions
        previous_positions[i] = agents[i];

        // Initialize bicycle model state
        vehicle_states[i].psi = agents[i].getSteering();
        vehicle_states[i].z = agents[i].getZ();  // forward direction
        vehicle_states[i].x = agents[i].getX();  // lateral direction
        vehicle_states[i].v_z = agents[i].getVz();  // forward direction
        vehicle_states[i].v_x = agents[i].getVx();  // lateral direction
        vehicle_states[i].yaw_rate = 0.0;
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
 * @param high_level_actions High-level actions for each agent.
 * @param low_level_actions Low-level actions for each agent.
 */
void Traffic::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {
    // Update positions of all agents
    for (auto& agent : agents) {
        updatePosition(agent, high_level_actions[agent.getId()], low_level_actions[agent.getId()]);
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
    int vehicle_id = vehicle.getId();

    // Store previous position
    previous_positions[vehicle_id] = vehicle;

    // Get current bicycle model state
    BicycleModel::VehicleState& current_state = vehicle_states[vehicle_id];

    // Process steering and acceleration inputs
    float steering = clamp(low_level_action[0], -0.610865f, 0.610865f);
    float acceleration = clamp(low_level_action[1], 0.0f, 4.5f);
    float braking = clamp(low_level_action[2], -8.0f, 0.0f);
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

    // Update vehicle state using enhanced dynamic bicycle model
    current_state = vehicle_models[vehicle_id].updateDynamicState(
        current_state,
        steering,
        net_acceleration,
        time_step
    );

    // Update vehicle properties based on bicycle model state
    vehicle.setX(current_state.x);  // lateral position
    vehicle.setZ(current_state.z);  // forward position
    vehicle.setSteering(current_state.psi);
    vehicle.setVx(current_state.v_x);
    vehicle.setVz(current_state.v_z);

    // Update bicycle model state for wraparound
    vehicle_states[vehicle_id].z = vehicle.getZ();
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
float Traffic::getMaxVelocity() const {
    return max_velocity;
}

/**
 * @brief Setter for the maximum velocity.
 * @param new_max_velocity New maximum velocity value. Must be positive.
*/
void Traffic::setMaxVelocity(float new_max_velocity) {
    if (new_max_velocity > 0) { // Ensure velocity is positive
        max_velocity = new_max_velocity;
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

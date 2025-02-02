#ifndef TRAFFIC_H
#define TRAFFIC_H

#include "vehicle.h"
#include "bicycle_model.h"

#include <vector>
#include <unordered_map>
#include <string>
#include <memory> // for std::shared_ptr
#include <random>


/**
 * @brief The Traffic class manages a simulation of multiple vehicles with
 *        their positions and velocities.
 */
class Traffic {
public:
    // Time step (assuming a fixed time step, adjust as necessary)
    float time_step = 0.04f; // e.g., 1.0f second or 1/25 for 25 FPS

    // Maximum velocity (m/s)
    float max_speed_;

    // Bicycle model to handle more realistic vehicle motion
    std::vector<BicycleModel> vehicle_models;

    /**
     * @brief Constructs a Traffic object with the specified number of agents.
     * @param num_agents Number of agents (vehicles) in the simulation.
     */
    Traffic(const int& num_agents, const unsigned& seed);

    /**
     * @brief Destructor to clean up resources, including perceptionModule.
     */
    ~Traffic();


    /**
     * @brief Samples and initializes agents with random positions and attributes.
     */
    void sampleAndInitializeAgents();

    /**
     * @brief Advances the simulation by one time step, updating agent positions and handling actions.
     * @param high_level_actions High-level actions for each agent.
     * @param low_level_actions Low-level actions for each agent.
     */
    void step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions);

    // Getters

    /**
     * @brief Retrieves all agents currently in the simulation.
     * @return Const reference to the vector of all agents.
     */
    const std::vector<Vehicle>& get_agents() const;

    /**
     * @brief Retrieves an agent by its name.
     * @param name The name of the agent to retrieve.
     * @return Reference to the agent with the specified name.
     * @throws std::runtime_error if the agent with the given name is not found.
     */
    const Vehicle& get_agent_by_name(const std::string& name) const;

    /**
     * @brief Retrieves positions of all agents.
     * @return Unordered map where keys are agent names and values are positions.
     */
    std::unordered_map<std::string, std::vector<float>> get_agent_positions() const;

    /**
     * @brief Retrieves velocities of all agents.
     * @return Unordered map where keys are agent names and values are velocities.
     */
    std::unordered_map<std::string, std::vector<float>> get_agent_velocities() const;

    /**
     * @brief Retrieves previous positions of all agents.
     * @return Unordered map where keys are agent names and values are previous positions.
     */
    std::unordered_map<std::string, std::vector<float>> get_previous_positions() const;

    /**
     * @brief Retrieves orientations of all agents.
     * @return Unordered map where keys are agent names and values are orientations.
     */
    std::unordered_map<std::string, std::vector<float>> get_agent_orientations() const;

    /**
     * @brief Getter for the time step.
     * @return Current time step value.
     */
    float getTimeStep() const;

    /**
     * @brief Getter for the maximum velocity.
     * @return Current maximum velocity value.
     */
    float getMaxVehicleSpeed() const;

    // Setters

    /**
     * @brief Setter for the time step.
     * @param new_time_step New time step value. Must be greater than 0.
     */
    void setTimeStep(float new_time_step);

    /**
     * @brief Setter for the maximum velocity.
     * @param new_max_velocity New maximum velocity value. Must be positive.
     */
    void setMaxVehicleSpeed(float max_speed);


private:
    int num_agents; ///< Number of agents in the simulation.
    std::vector<Vehicle> agents; ///< Vector of all agents in the simulation.
    std::vector<Vehicle> previous_positions; ///< Vector of previous positions of all agents.
    unsigned int seed;  ///< Seed value used in generator engine
    std::mt19937 generator;  ///< Separate generator for each vehicle. Alternative, std::default_random_engine

    // Helper functions

    /**
     * @brief Updates the position of a vehicle based on actions.
     * @param vehicle Reference to the vehicle to update.
     * @param high_level_action The high-level action to apply.
     * @param low_level_action The low-level actions to apply.
     */
    void applyActions(Vehicle& vehicle, int high_level_action, const std::vector<float>& low_level_action);

    /**
     * @brief Generates a random float within a specified range.
     * @param a Lower bound of the range.
     * @param b Upper bound of the range.
     * @return Random float within the specified range.
     */
    float randFloat(float a, float b);

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
    float randNormal(float mean, float stddev);
};


#endif // TRAFFIC_H

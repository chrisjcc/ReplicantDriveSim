#ifndef TRAFFIC_H
#define TRAFFIC_H

#include "vehicle.h"
#include "perception_module.h"

#include <vector>
#include <unordered_map>
#include <string>
#include <memory> // for std::shared_ptr


// Forward declaration of PerceptionModule
class PerceptionModule;


/**
 * @brief The Traffic class manages a simulation of multiple vehicles with
 *        their positions and velocities, along with collision detection and perception.
 */
class Traffic {
public:
    /**
     * @brief Constructs a Traffic object with the specified number of agents.
     * @param num_agents Number of agents (vehicles) in the simulation.
     */
    Traffic(int num_agents);

    /**
     * @brief Destructor to clean up resources, including perceptionModule.
     */
    ~Traffic();

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
    Vehicle& get_agent_by_name(const std::string& name);

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
     * @brief Retrieves nearby vehicles for a given agent.
     * @param agent_id The ID of the agent.
     * @return Vector of shared pointers to nearby vehicles.
     */
    std::vector<std::shared_ptr<Vehicle>> getNearbyVehicles(const std::string& agent_id) const;

private:
    int num_agents; ///< Number of agents in the simulation.
    std::vector<Vehicle> agents; ///< Vector of all agents in the simulation.
    std::vector<Vehicle> previous_positions; ///< Vector of previous positions of all agents.
    std::unique_ptr<PerceptionModule> perceptionModule; ///< Pointer to the PerceptionModule for perception calculations.

    // Helper functions

    /**
     * @brief Updates the position of a vehicle based on actions.
     * @param vehicle Reference to the vehicle to update.
     * @param high_level_action The high-level action to apply.
     * @param low_level_action The low-level actions to apply.
     */
    void updatePosition(Vehicle& vehicle, int high_level_action, const std::vector<float>& low_level_action);

    /**
     * @brief Checks for collisions between agents.
     * If two agents are within the vehicle width of each other, their velocities are set to zero.
     */
    void checkCollisions();

    /**
     * @brief Generates a random float within a specified range.
     * @param a Lower bound of the range.
     * @param b Upper bound of the range.
     * @return Random float within the specified range.
     */
    float randFloat(float a, float b);
};

#endif // TRAFFIC_H

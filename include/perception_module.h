#ifndef PERCEPTION_MODULE_H
#define PERCEPTION_MODULE_H

#pragma once

#include "vehicle.h"
#include "traffic_simulation.h"  // Include here for declaration purposes
#include <vector>
#include <string>

// Forward declaration of TrafficSimulation
class TrafficSimulation;

/**
 * @class PerceptionModule
 * @brief Represents the perception module that handles the detection of nearby vehicles and obstacles in the traffic simulation.
 */
class PerceptionModule {
public:
    /**
     * @brief Constructs a PerceptionModule object.
     * @param sim Reference to the traffic simulation object.
     * @param num_rays Number of rays used for perception.
     */
    explicit PerceptionModule(const TrafficSimulation& sim, int num_rays = 12);

    /**
     * @brief Destructor for PerceptionModule.
     */
    ~PerceptionModule();

    /**
     * @brief Updates the perception data for all agents in the simulation.
     */
    void updatePerceptions();

    /**
     * @brief Gets the observation data for a specific agent.
     * @param agent_name Name of the agent.
     * @return Vector of observation data for the agent.
     */
    std::vector<float> getAgentObservation(const std::string& agent_name) const;

    /**
     * @brief Detects nearby vehicles around the given ego vehicle.
     * @param ego_vehicle The vehicle for which to detect nearby vehicles.
     * @return Vector of shared pointers to detected nearby vehicles.
     */
    std::vector<std::shared_ptr<Vehicle>> detectNearbyVehicles(const Vehicle& ego_vehicle) const;

private:
    int numRays; ///< Number of rays used for perception.
    float deltaTheta; ///< Angle increment between rays.
    float rayAngleIncrement; ///< Angle increment for each ray.
    const TrafficSimulation& simulation; ///< Reference to the traffic simulation.

    /**
     * @brief Calculates the distance to the nearest obstacle for a given agent and ray angle.
     * @param agent The vehicle agent for which the distance is being calculated.
     * @param rayAngle The angle of the ray being cast.
     * @return Distance to the nearest obstacle.
     */
    float calculateDistanceToObstacle(const Vehicle& agent, float rayAngle) const;
};

#endif // PERCEPTION_MODULE_H

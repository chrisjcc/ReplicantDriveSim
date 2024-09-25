#ifndef PERCEPTION_MODULE_H
#define PERCEPTION_MODULE_H

#pragma once

#include "vehicle.h"
#include "traffic.h"

#include <vector>
#include <string>
#include <unordered_map>
#include <memory>


// Forward declaration of Traffic
class Traffic;


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
    explicit PerceptionModule(const Traffic& sim, int num_rays = 12);

    /**
     * @brief Destructor for PerceptionModule.
     */
    ~PerceptionModule();

    /**
     * @brief Gets the observation data for a specific agent.
     * @param agent The vehicle for which to get observations.
     * @return Vector of observation data for the agent.
     */
    const std::vector<float>& getObservations(const Vehicle& agent) const;

    /**
     * @brief Sets the observation data for all agents.
     * @param observations A map containing observation data for all agents.
     */
    void setObservations(const std::unordered_map<std::string, std::vector<float>>& observations);

    /**
     * @brief Updates the perception data for all agents in the simulation.
     */
    void updatePerceptions();

    /**
     * @brief Detects nearby vehicles around the given ego vehicle.
     * @param ego_vehicle The vehicle for which to detect nearby vehicles.
     * @return Vector of detected nearby vehicles.
     */
    std::vector<Vehicle> detectNearbyVehicles(const Vehicle& ego_vehicle) const;

private:
    int num_rays_; ///< Number of rays used for perception.
    const Traffic& simulation_; ///< Reference to the traffic simulation.
    std::unordered_map<std::string, std::vector<float>> observation_map_; ///< Map storing observation data for each agent.

    /**
     * @brief Calculates the distance to the nearest obstacle for a given agent and ray angle.
     * @param agent The vehicle agent for which the distance is being calculated.
     * @param ray_angle The angle of the ray being cast.
     * @return Distance to the nearest obstacle.
     */
    std::unordered_map<std::string, std::vector<float>> calculateDistanceToObstacles(const Vehicle& agent) const;
};

#endif // PERCEPTION_MODULE_H

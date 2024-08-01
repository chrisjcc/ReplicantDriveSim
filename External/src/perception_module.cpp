#include <iostream>
#include <cmath>
#include <algorithm>

#include "perception_module.h"
#include "traffic.h"

/**
 * @brief Constructs a PerceptionModule object.
 * @param sim Reference to the traffic simulation object.
 * @param num_rays Number of rays used for perception.
 */
PerceptionModule::PerceptionModule(const Traffic& sim, int num_rays)
    : num_rays_(num_rays), simulation_(sim) {}

/**
 * @brief Destructor for PerceptionModule.
 */
PerceptionModule::~PerceptionModule() {}

/**
 * @brief Updates the perception data for all agents in the simulation.
 */
void PerceptionModule::updatePerceptions() {
    // Iterate over agents and update their perceptions
    for (const auto& agent : simulation_.get_agents()) {
        auto observations = calculateDistanceToObstacles(agent);
        setObservations(observations);

        // Output retrieved observations
        std::cout << "Observations for " << agent.getName() << ": ";

        const auto& obs = getObservations(agent);

        for (const auto& val : obs) {
            std::cout << val << " ";
        }

        std::cout << std::endl;
    }
}

/**
 * @brief Gets the observation data for a specific agent.
 * @param agent The vehicle for which to get observations.
 * @return Vector of observation data for the agent.
 */
const std::vector<float>& PerceptionModule::getObservations(const Vehicle& agent) const {
    int id = agent.getId();

    auto it = observation_map_.find(id);

    if (it != observation_map_.end()) {
        return it->second;
    } else {
        // If not found, return an empty vector (assuming empty observations)
        static const std::vector<float> error_vector(12, -1.0f); // Default error value
        return error_vector;
    }
}

/**
 * @brief Sets the observation data for all agents.
 * @param observations A map containing observation data for all agents.
 */
void PerceptionModule::setObservations(const std::unordered_map<int, std::vector<float>>& observations) {
    for (const auto& observation : observations) {
        observation_map_[observation.first] = observation.second;
    }
}

/**
 * @brief Detects nearby vehicles around the given ego vehicle.
 * @param ego_vehicle The vehicle for which to detect nearby vehicles.
 * @return Vector of detected nearby vehicles.
 */
std::vector<Vehicle> PerceptionModule::detectNearbyVehicles(const Vehicle& ego_vehicle) const {
    std::vector<Vehicle> nearby_vehicles;

    for (const auto& vehicle : simulation_.get_agents()) {
        if (vehicle.getName() != ego_vehicle.getName()) {
            float distance = std::hypot(vehicle.getX() - ego_vehicle.getX(), vehicle.getY() - ego_vehicle.getY());
            if (distance <= vehicle.getSensorRange()) {
                nearby_vehicles.push_back(vehicle);
            }
        }
    }

    return nearby_vehicles;
}

/**
 * @brief Calculates the distance to the nearest obstacle for a given agent and ray angle.
 * @param agent The vehicle agent for which the distance is being calculated.
 * @return Map containing distances to the nearest obstacles for each ray.
 */
std::unordered_map<int, std::vector<float>> PerceptionModule::calculateDistanceToObstacles(const Vehicle& agent) const {
    std::unordered_map<int, std::vector<float>> distances_map;
    std::vector<float> distances(num_rays_, -999.0f); // Initialize all distances to -999

    float max_ray_length = 5000.0f; //100.0f; // Maximum distance for a ray
    float angle_increment = 2 * M_PI / num_rays_; // Angle increment for each ray

    for (int i = 0; i < num_rays_; ++i) {
        float ray_angle = i * angle_increment;
        float closest_distance = max_ray_length;

        float end_x = agent.getX() + max_ray_length * std::cos(ray_angle);
        float end_y = agent.getY() + max_ray_length * std::sin(ray_angle);

        for (const auto& other_agent : simulation_.get_agents()) {
            if (other_agent.getId() != agent.getId()) { // Avoid checking against itself
                float min_x = other_agent.getX() - other_agent.getLength() / 2.0f;
                float max_x = other_agent.getX() + other_agent.getLength() / 2.0f;
                float min_y = other_agent.getY() - other_agent.getWidth()  / 2.0f;
                float max_y = other_agent.getY() + other_agent.getWidth()  / 2.0f;

                float dx = end_x - agent.getX();
                float dy = end_y - agent.getY();
                float tmin = 0.0f;
                float tmax = 1.0f;

                if (std::fabs(dx) > 0.0001f) {
                    float tx1 = (min_x - agent.getX()) / dx;
                    float tx2 = (max_x - agent.getX()) / dx;
                    tmin = std::max(tmin, std::min(tx1, tx2));
                    tmax = std::min(tmax, std::max(tx1, tx2));
                }

                if (std::fabs(dy) > 0.0001f) {
                    float ty1 = (min_y - agent.getY()) / dy;
                    float ty2 = (max_y - agent.getY()) / dy;
                    tmin = std::max(tmin, std::min(ty1, ty2));
                    tmax = std::min(tmax, std::max(ty1, ty2));
                }

                if (tmax >= tmin && tmin >= 0.0f && tmin <= 1.0f) {
                    float intersection_x = agent.getX() + tmin * dx;
                    float intersection_y = agent.getY() + tmin * dy;
                    float distance = std::hypot(intersection_x - agent.getX(), intersection_y - agent.getY());
                    closest_distance = std::min(closest_distance, distance);
                }
            }
        }

        if (closest_distance < max_ray_length) {
            distances[i] = closest_distance;
        }
    }

    distances_map[agent.getId()] = distances;

    return distances_map;
}

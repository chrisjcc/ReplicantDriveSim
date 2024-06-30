#include "perception_module.h"
#include "traffic_simulation.h"

#include <cmath>


PerceptionModule::PerceptionModule(const TrafficSimulation& sim, int num_rays)
    : numRays(num_rays), delta_theta(2 * M_PI / num_rays), rayAngleIncrement(delta_theta), simulation(sim) {
    // Constructor implementation
}

PerceptionModule::~PerceptionModule() {
    // Destructor definition
}

void PerceptionModule::updatePerceptions() {
    // Iterate over agents and update their perceptions
    for (auto& agent : simulation.get_agents()) {
        std::vector<float> observations;
        for (int i = 0; i < numRays; ++i) {
            float rayAngle = agent.getSteering() + i * rayAngleIncrement;
            float distance = calculateDistanceToObstacle(agent, rayAngle);
            observations.push_back(distance);
        }
        // Store or use observations for further processing
        // Example: agent.setObservations(observations);
    }
}

std::vector<std::shared_ptr<Vehicle>> PerceptionModule::detectNearbyVehicles(const Vehicle& ego_vehicle) const {
    std::vector<std::shared_ptr<Vehicle>> nearby_vehicles;

    for (const auto& vehicle : simulation.get_agents()) {
        if (vehicle.getName() != ego_vehicle.getName()) {
            float distance = std::hypot(vehicle.getX() - ego_vehicle.getX(), vehicle.getY() - ego_vehicle.getY());
            if (distance <= vehicle.getSensorRange()) {
                nearby_vehicles.push_back(std::make_shared<Vehicle>(vehicle));
            }
        }
    }

    return nearby_vehicles;
}

std::vector<float> PerceptionModule::getAgentObservation(const std::string& agent_name) const {
    // Retrieve and return observations for a specific agent
    // Example: return simulation.getAgentByName(agent_name).getObservations();

    std::vector<float> observations(numRays, -1.0f); // Initialize with default value indicating no obstacle detected

    // Find the agent by name
    const Vehicle* agent = nullptr;

    for (const auto& veh : simulation.get_agents()) {
        if (veh.getName() == agent_name) {
            agent = &veh;
            break;
        }
    }

    if (agent == nullptr) {
        // Handle case where agent is not found (should not happen ideally)
        return observations;
    }

    // Generate observations using raycasting
    for (int i = 0; i < numRays; ++i) {
        float rayAngle = agent->getSteering() + i * rayAngleIncrement;
        float distance = calculateDistanceToObstacle(*agent, rayAngle);
        observations.push_back(distance);
    }

    return observations;
}

float PerceptionModule::calculateDistanceToObstacle(const Vehicle& agent, float rayAngle) const {
    // Implement raycasting logic to determine distance to obstacles
    // Placeholder implementation:
    // Here, you would perform raycasting to detect obstacles and calculate distances.
    // Example: For simplicity, return a placeholder distance.
    float maxRayLength = 100.0f; // Maximum distance for a ray

    // Compute endpoint of the ray
    float endX = agent.getX() + maxRayLength * std::cos(rayAngle);
    float endY = agent.getY() + maxRayLength * std::sin(rayAngle);

    // Here you would typically check intersections with obstacles, other agents, or boundaries
    // For simplicity, return a fixed distance
    // Placeholder implementation for collision detection with other agents (obstacles)
    float closestDistance = maxRayLength;

    for (const auto& otherAgent : simulation.get_agents()) {
        if (otherAgent.getName() != agent.getName()) { // Avoid checking against itself
            // Check if the ray intersects with the bounding box of the other agent
            // Bounding box coordinates
            float minX = otherAgent.getX() - otherAgent.getLength() / 2.0f;
            float maxX = otherAgent.getX() + otherAgent.getLength() / 2.0f;
            float minY = otherAgent.getY() - otherAgent.getWidth()  / 2.0f;
            float maxY = otherAgent.getY() + otherAgent.getWidth()  / 2.0f;

            // Check if the ray intersects the bounding box
            // Formula to calculate intersection point with the line segment
            float dx = endX - agent.getX();
            float dy = endY - agent.getY();
            float tmin = 0.0f;
            float tmax = 1.0f;

            if (std::fabs(dx) > 0.0001f) {
                float tx1 = (minX - agent.getX()) / dx;
                float tx2 = (maxX - agent.getX()) / dx;
                tmin = std::max(tmin, std::min(tx1, tx2));
                tmax = std::min(tmax, std::max(tx1, tx2));
            }

            if (std::fabs(dy) > 0.0001f) {
                float ty1 = (minY - agent.getY()) / dy;
                float ty2 = (maxY - agent.getY()) / dy;
                tmin = std::max(tmin, std::min(ty1, ty2));
                tmax = std::min(tmax, std::max(ty1, ty2));
            }

            // Check if there's a valid intersection within the ray length
            if (tmax >= tmin && tmin >= 0.0f && tmin <= 1.0f) {
                float intersectionX = agent.getX() + tmin * dx;
                float intersectionY = agent.getY() + tmin * dy;
                float distance = std::hypot(intersectionX - agent.getX(), intersectionY - agent.getY());
                closestDistance = std::min(closestDistance, distance);
            }
        }
    }

    return closestDistance;
}

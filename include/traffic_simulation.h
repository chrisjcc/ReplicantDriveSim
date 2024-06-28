#ifndef TRAFFIC_SIMULATION_H
#define TRAFFIC_SIMULATION_H

#include <vector>
#include <unordered_map>
#include <string>
#include <memory> // Added for std::shared_ptr
#include <random>

// Include order: standard headers, then local headers
#include "Lane.h"
#include "LaneSection.h"
#include "Math.hpp"
#include "Mesh.h"
#include "OpenDriveMap.h"
#include "RoadNetworkMesh.h"
#include "Road.h"
#include "collision_detection.h"

/**
 * @class TrafficSimulation
 * @brief Simulates traffic on a given road network.
 */
class TrafficSimulation {
public:
    /**
     * @struct Point
     * @brief Represents a 2D point.
     */
    struct Point {
        double x, y;
    };

    /**
     * @brief Constructs a TrafficSimulation object.
     * @param num_agents Number of agents in the simulation.
     * @param map_file Path to the OpenDrive map file.
     * @param cell_size Cell size for the spatial hash.
     * @param seed Seed value for the random number generator.
     */
    TrafficSimulation(int num_agents, const std::string& map_file, float cell_size, unsigned int seed = std::random_device{}());

    /**
     * @brief Advances the simulation by one step.
     * @param high_level_actions High-level actions for each agent.
     * @param low_level_actions Low-level actions for each agent.
     */
    void step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions);

    /**
     * @brief Gets the agents in the simulation.
     * @return A vector of tuples containing agent ID and position.
     */
    std::vector<Vehicle> get_agents() const;

    /**
     * @brief Gets the current positions of all agents.
     * @return A map from agent IDs to their positions.
     */
    std::unordered_map<std::string, std::vector<float>> get_agent_positions() const;

    /**
     * @brief Gets the current velocities of all agents.
     * @return A map from agent IDs to their velocities.
     */
    std::unordered_map<std::string, std::vector<float>> get_agent_velocities() const;

    /**
     * @brief Gets the previous positions of all agents.
     * @return A map from agent IDs to their previous positions.
     */
    std::unordered_map<std::string, std::vector<float>> get_previous_positions() const;

    /**
     * @brief Checks if a position is drivable.
     * @param position The position to check.
     * @return True if the position is drivable, false otherwise.
     */
    bool isPositionDrivable(const odr::Vec3D& position) const;

    /**
     * @brief Moves the vehicle to the nearest drivable point.
     * @param vehicle The vehicle to move.
     */
    void moveNearestDrivablePoint(Vehicle& vehicle);

    /**
     * @brief Gets the OpenDrive map.
     * @return A shared pointer to the OpenDrive map.
     */
    std::shared_ptr<odr::OpenDriveMap> get_odr_map() const;

    /**
     * @brief Sets the OpenDrive map.
     * @param map A shared pointer to the new OpenDrive map.
     */
    void set_odr_map(const std::shared_ptr<odr::OpenDriveMap>& map);

    // Public members
    SpatialHash spatialHash; ///< Spatial hash for efficient collision detection.
    std::shared_ptr<odr::OpenDriveMap> odr_map; ///< Shared pointer to the OpenDrive map.
    odr::RoadNetworkMesh road_network_mesh; ///< Road network mesh.

    std::vector<std::vector<Point>> drivable_areas; ///< Drivable areas in the simulation.

    /**
     * @brief Gets the seed value for the random number generator.
     * @return The seed value.
     */
    unsigned int getSeed() const;

    /**
     * @brief Calculates the heading of the road at a given s coordinate.
     * @param road The road object.
     * @param s The s coordinate along the road.
     * @return The heading in radians.
     */
    float get_heading(const odr::Road& road, const float s, const float t, const float h);

private:
    /**
     * @brief Initializes the position of a specific agent.
     * @param agent_index Index of the agent to be initialized.
     */
    void initializeAgentPosition(int agent_index);

    /**
     * @brief Updates the position of a vehicle.
     * @param vehicle The vehicle to update.
     * @param high_level_action The high-level action to apply.
     * @param low_level_action The low-level action to apply.
     */
    void updatePosition(Vehicle& vehicle, int high_level_action, const std::vector<float>& low_level_action);

    /**
     * @brief Checks for collisions between vehicles.
     * @param allPotentialCollisions Potential collisions to check.
     */
    void checkCollisions(const std::vector<std::pair<Vehicle*, std::vector<Vehicle*>>>& allPotentialCollisions);

    /**
     * @brief Checks if a point is inside a polygon.
     * @param point The point to check.
     * @param polygon The polygon to check against.
     * @return True if the point is inside the polygon, false otherwise.
     */
    bool pointInPolygon(const Point& point, const std::vector<Point>& polygon) const;

    /**
     * @brief Select a random point from a Normal distribution
     * @param mean The distribution mean.
     * @param std The distribution standard deviation (std).
     */
    float randNormal(float mean, float std);

    /**
     * @brief Generates a random float with a uniform distribution.
     * @param mean The lower end of the uniform distribution.
     * @param mean The upper end of the uniform distribution.
     * @return Random float sampled from the uniform distribution.
    */
    float randFloat(float a, float b);

    // Private members
    unsigned int seed;
    std::mt19937 gen;
    int num_agents; ///< Number of agents in the simulation.
    std::vector<Vehicle> agents; ///< List of agents in the simulation.
    std::vector<Vehicle> previous_positions; ///< Previous positions of the agents.
};
#endif // TRAFFIC_SIMULATION_H

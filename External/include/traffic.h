#ifndef TRAFFIC_H
#define TRAFFIC_H

#include "vehicle.h"
#include "perception_module.h"

#include <vector>
#include <unordered_map>
#include <string>
#include <memory>
#include <random>
#include <tuple>

#include "Lane.h"
#include "LaneSection.h"
#include "Math.hpp"
#include "Mesh.h"
#include "OpenDriveMap.h"
#include "RoadNetworkMesh.h"
#include "Road.h"

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
     * @param map_file Path to the OpenDrive map file.
     * @param seed Random seed for initializing the random number generator.
     */
    Traffic(const int& num_agents, const std::string& map_file, const unsigned& seed);

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

    /**
     * @brief Retrieves all agents currently in the simulation.
     * @return Const reference to the vector of all agents.
     */
    const std::vector<Vehicle>& getAgents() const;

    /**
     * @brief Retrieves an agent by its name.
     * @param name The name of the agent to retrieve.
     * @return Reference to the agent with the specified name.
     * @throws std::runtime_error if the agent with the given name is not found.
     */
    Vehicle& getAgentByName(const std::string& name);

    /**
     * @brief Retrieves positions of all agents.
     * @return Unordered map where keys are agent names and values are positions.
     */
    std::unordered_map<std::string, std::vector<float>> getAgentPositions() const;

    /**
     * @brief Retrieves velocities of all agents.
     * @return Unordered map where keys are agent names and values are velocities.
     */
    std::unordered_map<std::string, std::vector<float>> getAgentVelocities() const;

    /**
     * @brief Retrieves previous positions of all agents.
     * @return Unordered map where keys are agent names and values are previous positions.
     */
    std::unordered_map<std::string, std::vector<float>> getPreviousPositions() const;

    /**
     * @brief Retrieves orientations of all agents.
     * @return Unordered map where keys are agent names and values are orientations.
     */
    std::unordered_map<std::string, std::vector<float>> getAgentOrientations() const;

    /**
     * @brief Retrieves nearby vehicles for a given agent.
     * @param agent_id The ID of the agent.
     * @return Vector of nearby vehicles.
     */
    std::vector<Vehicle> getNearbyVehicles(const std::string& agent_id) const;

    /**
     * @brief Gets the OpenDrive map.
     * @return A shared pointer to the OpenDrive map.
     */
    std::shared_ptr<odr::OpenDriveMap> getOdrMap() const;

    /**
     * @brief Sets the OpenDrive map.
     * @param map A shared pointer to the new OpenDrive map.
     */
    void setOdrMap(const std::shared_ptr<odr::OpenDriveMap>& map);

    /**
     * @brief Calculates the heading of the road at a given s coordinate.
     * @param road_position The vehicle position (x, y, z).
     * @return The heading in radians.
     */
    std::tuple<float, int> getHeading(const odr::Vec3D& road_position);

    /**
     * @brief Projects a 3D point (x, y, z) onto the road's s-t coordinate system.
     *
     * This method takes a 3D point in the global coordinate system and projects it onto
     * the road's local s-t coordinate system. It finds the closest point on the road to
     * the given (x, y, z) point and calculates the corresponding s (longitudinal) and
     * t (lateral) coordinates.
     *
     * @param road The road object onto which the point is being projected.
     * @param xy The 3D point in the global coordinate system to be projected.
     * @param s [out] The calculated s-coordinate (longitudinal distance along the road).
     * @param t [out] The calculated t-coordinate (lateral offset from the road's reference line).
     * @param projected_pos [out] The 3D position of the projected point on the road.
     *
     * @note This method uses a sampling approach to find the closest point on the road.
     *       The accuracy of the projection depends on the number of samples used.
     *
     * @throws std::runtime_error If there's an error in accessing road geometry or if
     *         the projection falls outside the valid road length.
     */
    void projectXyToSt(const odr::Road& road, const odr::Vec3D& xy, float& s, float& t, odr::Vec3D& projected_pos);

    /**
     * @brief Checks if a given position is drivable.
     * @param position The 3D position to check.
     * @return True if the position is drivable, false otherwise.
     */
    bool isPositionDrivable(const odr::Vec3D& position);

    /**
     * @brief Moves the vehicle to the nearest drivable point.
     * @param vehicle Reference to the vehicle to be moved.
     */
    void moveNearestDrivablePoint(Vehicle& vehicle);

    /**
     * @brief Finds the nearest road point for a given position.
     * @param position The 3D position to check.
     * @return A pair containing a pointer to the nearest road and the nearest point on that road.
     */
    std::pair<const odr::Road*, odr::Vec3D> getNearestRoadPoint(const odr::Vec3D& position) const;

    /**
     * @brief Calculates the tangent vector of the road at a given s coordinate.
     * @param road The road object.
     * @param s The s-coordinate along the road.
     * @return The tangent vector at the given s coordinate.
     */
    odr::Vec3D getTangent(const odr::Road& road, double s) const;

    /**
     * @brief Calculates the normal vector of the road at a given s coordinate.
     * @param road The road object.
     * @param s The s-coordinate along the road.
     * @return The normal vector at the given s coordinate.
     */
    odr::Vec3D getNormal(const odr::Road& road, double s) const;

    /**
     * @brief Projects a point onto the road and returns the s coordinate.
     * @param road The road object.
     * @param position The 3D position to project.
     * @return The s coordinate on the road.
     */
    double projectPointOntoRoad(const odr::Road* road, const odr::Vec3D& position);

    /**
     * @brief Calculates the lateral offset of a position from the road's reference line.
     * @param road The road object.
     * @param s The s coordinate along the road.
     * @param position The 3D position to check.
     * @return The lateral offset from the road's reference line.
     */
    double calculateLateralOffset(const odr::Road* road, double s, const odr::Vec3D& position);

    /**
     * @brief Converts Cartesian coordinates (x, y) to Frenet coordinates (s, t).
     * @param x The x-coordinate in the Cartesian system.
     * @param y The y-coordinate in the Cartesian system.
     * @return A pair containing the s and t coordinates in the Frenet system.
     */
    std::pair<double, double> convertToFrenetCoordinates(double x, double y);

private:
    float time_step = 0.04f; ///< Time step for the simulation.

    int num_agents; ///< Number of agents in the simulation.
    std::vector<Vehicle> agents; ///< Vector of all agents in the simulation.
    std::vector<Vehicle> previous_positions; ///< Vector of previous positions of all agents.
    std::unique_ptr<PerceptionModule> perception_module; ///< Pointer to the PerceptionModule for perception calculations.
    std::shared_ptr<odr::OpenDriveMap> odr_map; ///< Shared pointer to the OpenDrive map.
    std::vector<odr::Road> roads; ///< Roads found on the OpenDrive map.
    std::mt19937 generator; ///< Mersenne Twister random number generator.

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

    /**
     * @brief Generates a random float following a normal (Gaussian) distribution.
     * @param mean The mean (average) of the normal distribution.
     * @param stddev The standard deviation of the normal distribution.
     * @return Random float following the specified normal distribution.
     */
    float randNormal(float mean, float stddev);
};

#endif // TRAFFIC_H

#include "traffic_simulation.h"
#include <iostream>
#include <vector>
#include <tuple>
#include <algorithm>
#include <cmath>
#include <climits>
#include <cstdlib>
#include <random>
#include "collision_detection.h"

// Custom clamp function for C++11
template <typename T>
T clamp(T value, T min_val, T max_val) {
    return std::max(min_val, std::min(value, max_val));
}

/**
 * @brief Constructor for TrafficSimulation.
 * @param num_agents Number of agents in the simulation.
 * @param map_file Path to the OpenDRIVE map file.
 * @param cell_size Size of each cell in the spatial hash grid.
 */
TrafficSimulation::TrafficSimulation(int num_agents, const std::string& map_file, float cell_size)
    : spatialHash(cell_size),
      odr_map(std::make_shared<odr::OpenDriveMap>(map_file)),
      road_network_mesh(odr_map->get_road_network_mesh(0.1)),
      num_agents(num_agents)
{
    agents.resize(num_agents);
    previous_positions.resize(num_agents);

    // Get all roads from the OpenDRIVE map
    std::vector<odr::Road> roads = odr_map->get_roads();

    // Initialize agent positions
    for (int i = 0; i < num_agents; ++i) {
        // Randomly select a road
        odr::Road& road = roads[rand() % roads.size()];

        // Randomly select a lane section
        std::vector<odr::LaneSection> lane_sections = road.get_lanesections();
        size_t lane_section_index = rand() % lane_sections.size();
        auto& lane_section = lane_sections[lane_section_index];

        // Determine the end position of the lane section
        float s0 = lane_section.s0;
        float s1 = (lane_section_index == lane_sections.size() - 1) ? road.length : lane_sections[lane_section_index + 1].s0;

        // Get drivable lanes (excluding sidewalks, etc.)
        std::vector<odr::Lane*> drivable_lanes;
        for (auto& lane_pair : lane_section.id_to_lane) {
            odr::Lane* lane = &lane_pair.second;
            if (lane->type == "driving" || lane->type == "exit" || lane->type == "entry") {
                drivable_lanes.push_back(lane);
            }
        }

        // Randomly select a drivable lane
        odr::Lane* lane = drivable_lanes[rand() % drivable_lanes.size()];

        // Sample a position along the lane
        float s = randFloat(s0, s1);
        float lane_width = 3.5 + 0.01 * (s - 0.01) + 0.001 * std::pow(s - 0.01, 2) + 0.001 * std::pow(s - 0.01, 3);
        float t = randFloat(0, lane_width / 2);
        if (lane->id < 0) t = -t;

        // Convert lane coordinates to world coordinates
        odr::Vec3D position = road.get_xyz(s, t, 0.0);

        agents[i].setX(position[0]);
        agents[i].setY(position[1]);
        agents[i].setVx(randNormal(50.0f, 1.0f));
        agents[i].setVy(0.0f);
        agents[i].setSteering(0.0f);
        agents[i].setName("agent_" + std::to_string(i));
        agents[i].setWidth(2.0f);
        agents[i].setLength(5.0f);
        agents[i].setId(i);

        previous_positions[i] = agents[i];
    }
}

/**
 * @brief Updates the simulation by one time step.
 * @param high_level_actions Vector of high-level actions for each agent.
 * @param low_level_actions Vector of low-level actions for each agent.
 */
void TrafficSimulation::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {
    spatialHash.clear();
    std::vector<std::pair<Vehicle*, std::vector<Vehicle*>>> allPotentialCollisions;

    for (auto& agent : agents) {
        updatePosition(agent, high_level_actions[agent.getId()], low_level_actions[agent.getId()]);

        odr::Vec3D position{agent.getX(), agent.getY(), 0.0};

        if (!isPositionDrivable(position)) {
            moveNearestDrivablePoint(agent);
        }

        spatialHash.insert(&agent);
        auto potentialCollisions = spatialHash.getPotentialCollisions(&agent);
        allPotentialCollisions.push_back({&agent, potentialCollisions});
    }

    checkCollisions(allPotentialCollisions);
}

/**
 * @brief Gets the agents in the simulation.
 * @return A vector of tuples containing agent ID and position.
 */
std::vector<std::tuple<std::string, std::vector<float>>> TrafficSimulation::get_agents() const {
    std::vector<std::tuple<std::string, std::vector<float>>> agent_data;
    for (const auto& agent : agents) {
        std::vector<float> position = {agent.getX(), agent.getY(), agent.getZ()};
        agent_data.push_back(std::make_tuple(agent.getName(), position));
    }
    return agent_data;
}

/**
 * @brief Gets the positions of all agents.
 * @return A map of agent names to their positions.
 */
std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_agent_positions() const {
    std::unordered_map<std::string, std::vector<float>> positions;
    for (int i = 0; i < num_agents; ++i) {
        positions["agent_" + std::to_string(i)] = {agents[i].getX(), agents[i].getY()};
    }
    return positions;
}

/**
 * @brief Gets the velocities of all agents.
 * @return A map of agent names to their velocities.
 */
std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_agent_velocities() const {
    std::unordered_map<std::string, std::vector<float>> velocities;
    for (int i = 0; i < num_agents; ++i) {
        velocities["agent_" + std::to_string(i)] = {agents[i].getVx(), agents[i].getVy()};
    }
    return velocities;
}

/**
 * @brief Gets the previous positions of all agents.
 * @return A map of agent names to their previous positions.
 */
std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_previous_positions() const {
    std::unordered_map<std::string, std::vector<float>> previous_positions_map;
    for (int i = 0; i < num_agents; ++i) {
        previous_positions_map["agent_" + std::to_string(i)] = {previous_positions[i].getX(), previous_positions[i].getY()};
    }
    return previous_positions_map;
}

/**
 * @brief Updates the position and velocity of a vehicle based on actions.
 * @param vehicle The vehicle to be updated.
 * @param high_level_action High-level action for the vehicle.
 * @param low_level_action Low-level actions for the vehicle.
 */
void TrafficSimulation::updatePosition(Vehicle &vehicle, int high_level_action, const std::vector<float>& low_level_action) {
    vehicle.setSteering(clamp(low_level_action[0], -0.610865f, 0.610865f));
    float acceleration = clamp(low_level_action[1], 0.0f, 4.5f);
    float braking = clamp(low_level_action[2], -8.0f, 0.0f);

    vehicle.setVx(vehicle.getVx() + (acceleration - braking));
    vehicle.setVy(0.0f);
    const float max_velocity = 10.0f;
    vehicle.setVx(clamp(vehicle.getVx(), 0.0f, max_velocity));

    vehicle.setX(vehicle.getX() + vehicle.getVx() * std::cos(vehicle.getSteering()));
    vehicle.setY(vehicle.getY() + vehicle.getVx() * std::sin(vehicle.getSteering()));
}

/**
 * @brief Checks for collisions among all vehicles.
 * @param allPotentialCollisions Vector of potential collision pairs.
 */
void TrafficSimulation::checkCollisions(const std::vector<std::pair<Vehicle*, std::vector<Vehicle*>>>& allPotentialCollisions) {
    for (const auto& pair : allPotentialCollisions) {
        Vehicle* vehicle = pair.first;
        const std::vector<Vehicle*>& potentialCollisions = pair.second;

        for (const auto* other : potentialCollisions) {
            if (vehicle != other && std::hypot(vehicle->getX() - other->getX(), vehicle->getY() - other->getY()) < vehicle->getWidth()) {
                vehicle->setVx(0.0f);
                vehicle->setVy(0.0f);
                const_cast<Vehicle*>(other)->setVx(0.0f);
                const_cast<Vehicle*>(other)->setVy(0.0f);

                std::cout << "COLLISION DETECTED between " << vehicle->getName() << " and " << other->getName() << std::endl;
            }
        }
    }
}

/**
 * @brief Generates a random float between a and b.
 * @param a Lower bound.
 * @param b Upper bound.
 * @return Random float between a and b.
 */
float TrafficSimulation::randFloat(float a, float b) {
    static std::random_device rd;
    static std::mt19937 gen(rd());
    std::uniform_real_distribution<> dis(a, b);
    return dis(gen);
}

/**
 * @brief Checks if a given position is drivable.
 * @param position The position to check.
 * @return True if the position is drivable, false otherwise.
 */
bool TrafficSimulation::isPositionDrivable(const odr::Vec3D& position) const {
    // Create a point position p(x, y)
    Point p{position[0], position[1]};
    for (const auto& polygon : drivable_areas) {
        if (pointInPolygon(p, polygon)) {
            return true;
        }
    }
    return false;
}

/**
 * @brief Moves a vehicle to the nearest drivable point if it's out of bounds.
 * @param vehicle The vehicle to move.
 */
void TrafficSimulation::moveNearestDrivablePoint(Vehicle& vehicle) {
    // Create a point position p(x, y)
    Point position{vehicle.getX(), vehicle.getY()};

    double min_distance = std::numeric_limits<double>::max();
    Point nearest_point;

    for (const auto& polygon : drivable_areas) {
        for (const auto& vertex : polygon) {
            double distance = std::hypot(position.x - vertex.x, position.y - vertex.y);
            if (distance < min_distance) {
                min_distance = distance;
                nearest_point = vertex;
            }
        }
    }
    vehicle.setX(nearest_point.x);
    vehicle.setY(nearest_point.y);
}

/**
 * @brief Checks if a given point is inside a polygon.
 * @param point The point to check.
 * @param polygon The polygon to check against.
 * @return True if the point is inside the polygon, false otherwise.
 */
bool TrafficSimulation::pointInPolygon(const Point& point, const std::vector<Point>& polygon) const {
    bool inside = false;
    for (size_t i = 0, j = polygon.size() - 1; i < polygon.size(); j = i++) {
        if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
            (point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)) {
            inside = !inside;
        }
    }
    return inside;
}

/**
 * @brief Gets the OpenDRIVE map associated with the simulation.
 * @return Shared pointer to the OpenDRIVE map.
 */
std::shared_ptr<odr::OpenDriveMap> TrafficSimulation::get_odr_map() const {
    return odr_map;
}

/**
 * @brief Sets the OpenDRIVE map for the simulation.
 * @param map Shared pointer to the OpenDRIVE map.
 */
void TrafficSimulation::set_odr_map(const std::shared_ptr<odr::OpenDriveMap>& map) {
    odr_map = map;
}

/**
 * @brief Generates a random float with a normal distribution.
 * @param mean The mean of the normal distribution.
 * @param stddev The standard deviation of the normal distribution.
 * @return Random float sampled from the normal distribution.
 */
float TrafficSimulation::randNormal(float mean, float stddev) {
    static std::random_device rd;
    static std::mt19937 gen(rd());
    std::normal_distribution<> dis(mean, stddev);
    return dis(gen);
}

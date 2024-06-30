#include "traffic_simulation.h"
#include "perception_module.h"
#include <algorithm> // for std::max and std::min
#include <cmath>
#include <random>

const int SCREEN_WIDTH = 900;
const int SCREEN_HEIGHT = 400;
const int VEHICLE_WIDTH = 130;
const int VEHICLE_HEIGHT = 55;
const int LANE_WIDTH = 100;
const int NUM_LANES = 3;

// Custom clamp function for C++11
template <typename T>
T clamp(T value, T min_val, T max_val) {
    return std::max(min_val, std::min(value, max_val));
}

// Function to generate a random float within a specified range
float randFloat(float a, float b) {
    static std::default_random_engine generator;
    std::uniform_real_distribution<float> distribution(a, b);
    return distribution(generator);
}

// Function to generate a random float from a normal distribution
float randNormal(float mean, float stddev) {
    static std::default_random_engine generator;
    std::normal_distribution<float> distribution(mean, stddev);
    return distribution(generator);
}


TrafficSimulation::TrafficSimulation(int num_agents) : num_agents(num_agents) {
    agents.resize(num_agents);
    previous_positions.resize(num_agents);

    perceptionModule = new PerceptionModule(*this); // Initialize the pointer

    for (int i = 0; i < num_agents; ++i) {
        agents[i].x = randFloat(0, SCREEN_WIDTH - VEHICLE_WIDTH);
        agents[i].y = randFloat(0, SCREEN_HEIGHT - VEHICLE_HEIGHT);
        agents[i].z = 0.0f;
        agents[i].vx = randNormal(50.0f, 1.0f);  // Randomly sample initial speed
        agents[i].vy = 0.0f;                     // Initial vertical velocity
        agents[i].vz = 0.0f;
        agents[i].steering = 0.0f;
        agents[i].name = "agent_" + std::to_string(i);
        agents[i].width = 2.0f;
        agents[i].length = 5.0f;

        // Initialize previous positions with current positions
        previous_positions[i] = agents[i];
    }
}

TrafficSimulation::~TrafficSimulation() {
    delete perceptionModule; // Clean up the pointer in the destructor
}

void TrafficSimulation::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {
    for (int i = 0; i < num_agents; ++i) {
        applyAction(i, high_level_actions[i], low_level_actions[i]);
    }

    // Update perceptions
    perceptionModule->updatePerceptions();

    // Update vehicle position
    updatePositions();

    // Check for collisions between agents
    checkCollisions();
}

const std::vector<Vehicle>& TrafficSimulation::get_agents() const { return agents; }

Vehicle& TrafficSimulation::get_agent_by_name(const std::string& name) {
    auto it = std::find_if(agents.begin(), agents.end(),
                           [&name](const Vehicle& agent) {
                               return agent.name == name;
                           });

    if (it != agents.end()) {
        return *it;
    } else {
        // Handle the case where the agent is not found
        throw std::runtime_error("Agent with name " + name + " not found.");
    }
}

std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_agent_positions() const {
    std::unordered_map<std::string, std::vector<float>> positions;
    for (int i = 0; i < num_agents; ++i) {
        positions["agent_" + std::to_string(i)] = {agents[i].x, agents[i].y, agents[i].z};
    }
    return positions;
}

std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_agent_velocities() const {
    std::unordered_map<std::string, std::vector<float>> velocities;
    for (int i = 0; i < num_agents; ++i) {
        velocities["agent_" + std::to_string(i)] = {agents[i].vx, agents[i].vy, agents[i].vz};
    }
    return velocities;
}

std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_previous_positions() const {
    std::unordered_map<std::string, std::vector<float>> previous_positions_map;
    for (int i = 0; i < num_agents; ++i) {
        previous_positions_map["agent_" + std::to_string(i)] = {previous_positions[i].x, previous_positions[i].y, previous_positions[i].z};
    }
    return previous_positions_map;
}

void TrafficSimulation::applyAction(int agent_idx, int high_level_action, const std::vector<float>& low_level_action) {
    auto& agent = agents[agent_idx];

    // Apply steering
    agent.steering += low_level_action[0];
    agent.steering = clamp(agent.steering, -0.610865f, 0.610865f);
    float acceleration = clamp(low_level_action[1], 0.0f, 4.5f); // acceleration
    float braking = clamp(low_level_action[2], -8.0f, 0.0f); // braking

    switch (high_level_action) {
        case 0: // Keep lane
            break;
        case 1: // Left lane change
            agent.y -= LANE_WIDTH;
            agent.y = std::fmin(std::fmax(agent.y, LANE_WIDTH), (NUM_LANES - 1) * LANE_WIDTH);
            break;
        case 2: // Right lane change
            agent.y += LANE_WIDTH;
            agent.y = std::fmin(std::fmax(agent.y, LANE_WIDTH), (NUM_LANES - 1) * LANE_WIDTH);
            break;
        case 3: // Accelerate
            agent.vx += acceleration;
            break;
        case 4: // Decelerate
            agent.vx -= braking;
            break;
    }

    // Clamp velocities
    const float max_velocity = 10.0f;
    agent.vx = std::fmin(std::fmax(agent.vx, 0.0f), max_velocity);
    agent.vy = std::fmin(std::fmax(agent.vy, -max_velocity), max_velocity);
}

void TrafficSimulation::updatePositions() {
    for (auto& agent : agents) {
        agent.x += agent.vx;
        agent.y += agent.vy;

        // Wrap around horizontally
        if (agent.x < 0) agent.x += SCREEN_WIDTH;
        if (agent.x >= SCREEN_WIDTH) agent.x -= SCREEN_WIDTH;

        // Constrain vertically within the road
        agent.y = std::fmin(std::fmax(agent.y, LANE_WIDTH), (NUM_LANES - 1) * LANE_WIDTH);
    }
}

std::vector<std::shared_ptr<Vehicle>> TrafficSimulation::getNearbyVehicles(const std::string& agent_name) const {
    // Find the vehicle corresponding to the given agent ID
    const Vehicle* ego_vehicle = nullptr;
    for (const auto& vehicle : agents) {
        if (vehicle.name == agent_name) {
            ego_vehicle = &vehicle;
            break;
        }
    }

    // If the vehicle with the given agent ID is not found, return an empty vector
    if (ego_vehicle == nullptr) {
        return std::vector<std::shared_ptr<Vehicle>>();
    }

    // Call the PerceptionModule method to detect nearby vehicles
    return perceptionModule->detectNearbyVehicles(*ego_vehicle);
}

void TrafficSimulation::checkCollisions() {
    for (int i = 0; i < num_agents; ++i) {
        for (int j = i + 1; j < num_agents; ++j) {
            if (std::hypot(agents[i].x - agents[j].x, agents[i].y - agents[j].y) < VEHICLE_WIDTH) {
                // Handle collision
                agents[i].vx = agents[i].vy = 0.0f;
                agents[j].vx = agents[j].vy = 0.0f;
            }
        }
    }
}

float TrafficSimulation::randFloat(float a, float b) {
    return a + static_cast<float>(rand()) / (static_cast<float>(RAND_MAX / (b - a)));
}

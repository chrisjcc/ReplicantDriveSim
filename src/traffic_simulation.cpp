#include "traffic_simulation.h"
#include "perception_module.h"
#include <algorithm>
#include <cmath>
#include <random>

const int SCREEN_WIDTH = 900;
const int SCREEN_HEIGHT = 400;
const int VEHICLE_WIDTH = 130;
const int VEHICLE_HEIGHT = 55;
const int LANE_WIDTH = 100;
const int NUM_LANES = 3;

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

TrafficSimulation::TrafficSimulation(int num_agents)
    : num_agents(num_agents), agents(num_agents), previous_positions(num_agents){

    perceptionModule = new PerceptionModule(*this); // Initialize the pointer

    for (int i = 0; i < num_agents; ++i) {
        agents[i].x = randFloat(0, SCREEN_WIDTH - VEHICLE_WIDTH);
        agents[i].y = randFloat(0, SCREEN_HEIGHT - VEHICLE_HEIGHT);
        agents[i].vx = randNormal(50.0f, 1.0f);  // Randomly sample initial speed
        agents[i].vy = 0.0f;                     // Initial vertical velocity
        agents[i].steering = 0.0f;
        agents[i].name = "agent_" + std::to_string(i);

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

    updatePositions();
    // Check for collisions between agents
    checkCollisions();
}

std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_agent_positions() const {
    std::unordered_map<std::string, std::vector<float>> positions;
    for (int i = 0; i < num_agents; ++i) {
        positions["agent_" + std::to_string(i)] = {agents[i].x, agents[i].y};
    }
    return positions;
}

std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_agent_velocities() const {
    std::unordered_map<std::string, std::vector<float>> velocities;
    for (int i = 0; i < num_agents; ++i) {
        velocities["agent_" + std::to_string(i)] = {agents[i].vx, agents[i].vy};
    }
    return velocities;
}

std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_previous_positions() const {
    std::unordered_map<std::string, std::vector<float>> previous_positions_map;
    for (int i = 0; i < num_agents; ++i) {
        previous_positions_map["agent_" + std::to_string(i)] = {previous_positions[i].x, previous_positions[i].y};
    }
    return previous_positions_map;
}

void TrafficSimulation::applyAction(int agent_idx, int high_level_action, const std::vector<float>& low_level_action) {
    auto& agent = agents[agent_idx];

    switch (high_level_action) {
        case 0: // Keep lane
            break;
        case 1: // Change lane
            agent.y += LANE_WIDTH;
            agent.y = std::fmin(std::fmax(agent.y, LANE_WIDTH), (NUM_LANES - 1) * LANE_WIDTH);
            break;
        case 2: // Accelerate
            agent.vy += low_level_action[1];
            break;
        case 3: // Decelerate
            agent.vy -= low_level_action[2];
            break;
    }

    // Apply steering
    agent.steering += low_level_action[0];

    // Clamp velocities
    const float max_velocity = 10.0f;
    agent.vx = std::fmin(std::fmax(agent.vx, -max_velocity), max_velocity);
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

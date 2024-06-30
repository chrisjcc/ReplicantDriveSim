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
        // Set agent attributes
        agents[i].setX(randFloat(0, SCREEN_WIDTH - VEHICLE_WIDTH));
        agents[i].setY(randFloat(0, SCREEN_HEIGHT - VEHICLE_HEIGHT));
        agents[i].setZ(0.0f);
        agents[i].setVx(randNormal(50.0f, 1.0f)); // Randomly sample initial speed
        agents[i].setVy(randNormal(0.0f, 1.0f));  // Initial lateral velocity
        agents[i].setVz(0.0f);
        agents[i].setSteering(randNormal(0.0f, 1.0f));
        agents[i].setId(i);
        agents[i].setName("agent_" + std::to_string(i));
        agents[i].setWidth(2.0f);
        agents[i].setLength(5.0f);

        // Initialize previous positions with current positions
        previous_positions[i] = agents[i];
    }
}

TrafficSimulation::~TrafficSimulation() {
    delete perceptionModule; // Clean up the pointer in the destructor
}

void TrafficSimulation::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {

    for (auto& agent : agents) {
        updatePosition(agent, high_level_actions[agent.getId()], low_level_actions[agent.getId()]);

    }
    //for (int i = 0; i < num_agents; ++i) {
    //    applyAction(i, high_level_actions[i], low_level_actions[i]);
    //}

    // Update perceptions
    perceptionModule->updatePerceptions();

    // Update vehicle position
    //updatePositions();

    // Check for collisions between agents
    //checkCollisions();
}

const std::vector<Vehicle>& TrafficSimulation::get_agents() const { return agents; }

Vehicle& TrafficSimulation::get_agent_by_name(const std::string& name) {
    auto it = std::find_if(agents.begin(), agents.end(),
                           [&name](const Vehicle& agent) {
                               return agent.getName() == name;
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
        positions["agent_" + std::to_string(i)] = {agents[i].getX(), agents[i].getY(), agents[i].getZ()};
    }
    return positions;
}

std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_agent_velocities() const {
    std::unordered_map<std::string, std::vector<float>> velocities;
    for (int i = 0; i < num_agents; ++i) {
        velocities["agent_" + std::to_string(i)] = {agents[i].getVx(), agents[i].getVy(), agents[i].getVz()};
    }
    return velocities;
}

std::unordered_map<std::string, std::vector<float>> TrafficSimulation::get_previous_positions() const {
    std::unordered_map<std::string, std::vector<float>> previous_positions_map;
    for (int i = 0; i < num_agents; ++i) {
        previous_positions_map["agent_" + std::to_string(i)] = {previous_positions[i].getX(), previous_positions[i].getY(), previous_positions[i].getZ()};
    }
    return previous_positions_map;
}

void TrafficSimulation::updatePosition(Vehicle &vehicle, int high_level_action, const std::vector<float>& low_level_action) {
    // Bound kinematics to physical constraints
    float steering = clamp(low_level_action[0], -0.610865f, 0.610865f); // Clamp steering between -35 and 35 degrees in radians
    vehicle.setSteering(steering);

    float acceleration = clamp(low_level_action[1], 0.0f, 4.5f); // Acceleration (m/s^2)
    float braking = clamp(low_level_action[2], -8.0f, 0.0f); // Braking deceleration (m/s^2)

    float net_acceleration = acceleration + braking; // Net acceleration considering both acceleration and braking

    // Time step (assuming a fixed time step, adjust as necessary)
    float time_step = 0.04f; // e.g., 1.0f second or 1/25 for 25 FPS

    const float max_velocity = 60.0f; // Maximum velocity (m/s)

    float initial_velocity_x = vehicle.getVx();
    float initial_velocity_y = vehicle.getVy();

    // Calculate the components of the net acceleration in the x and y directions
    float acceleration_x = net_acceleration * std::cos(steering);
    float acceleration_y = net_acceleration * std::sin(steering);

    // Update the velocities
    float new_velocity_x = clamp(initial_velocity_x + acceleration_x * time_step, 0.0f, max_velocity);
    float new_velocity_y = clamp(initial_velocity_y + acceleration_y * time_step, 0.0f, max_velocity);

    vehicle.setVx(new_velocity_x);
    vehicle.setVy(new_velocity_y);

    // Update the position using the kinematic equations
    float delta_x = initial_velocity_x * time_step + 0.5f * acceleration_x * time_step * time_step;
    float delta_y = initial_velocity_y * time_step + 0.5f * acceleration_y * time_step * time_step;

    float new_x = vehicle.getX() + delta_x;
    float new_y = vehicle.getY() + delta_y;

    vehicle.setX(new_x);
    vehicle.setY(new_y);


    // Wrap around horizontally
    if (vehicle.getX() < 0) vehicle.setX(vehicle.getX() + SCREEN_WIDTH);
    if (vehicle.getX() >= SCREEN_WIDTH) vehicle.setX(vehicle.getX() - SCREEN_WIDTH);

    // Constrain vertically within the road
    //vehicle.y = std::fmin(std::fmax(vehicle.y, LANE_WIDTH), (NUM_LANES - 1) * LANE_WIDTH);

    switch (high_level_action) {
        case 0: // Keep lane
            break;
        case 1: // Left lane change
            vehicle.setY(vehicle.getY() - LANE_WIDTH);
            vehicle.setY(std::fmin(std::fmax(vehicle.getY(), LANE_WIDTH), (NUM_LANES - 1) * LANE_WIDTH));
            break;
        case 2: // Right lane change
            vehicle.setY(vehicle.getY() + LANE_WIDTH);
            vehicle.setY(std::fmin(std::fmax(vehicle.getY(), LANE_WIDTH), (NUM_LANES - 1) * LANE_WIDTH));
            break;
        case 3: // Accelerate
            vehicle.setVx(vehicle.getVx() + acceleration);
            break;
        case 4: // Decelerate
            vehicle.setVx(vehicle.getVx() - braking);
            break;
    }
}

std::vector<std::shared_ptr<Vehicle>> TrafficSimulation::getNearbyVehicles(const std::string& agent_name) const {
    // Find the vehicle corresponding to the given agent ID
    const Vehicle* ego_vehicle = nullptr;
    for (const auto& vehicle : agents) {
        if (vehicle.getName() == agent_name) {
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
            if (std::hypot(agents[i].getX() - agents[j].getX(), agents[i].getY() - agents[j].getY()) < VEHICLE_WIDTH) {
                // Handle collision
                agents[i].setVx(0.0f);
                agents[i].setVy(0.0f);
                agents[j].setVx(0.0f);
                agents[j].setVy(0.0f);
            }
        }
    }
}

float TrafficSimulation::randFloat(float a, float b) {
    return a + static_cast<float>(rand()) / (static_cast<float>(RAND_MAX / (b - a)));
}

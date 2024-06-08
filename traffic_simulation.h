#ifndef TRAFFIC_SIMULATION_H
#define TRAFFIC_SIMULATION_H

#include <vector>
#include <map>
#include <string>
#include <array>

class Vehicle {
public:
    float x, y;
    float vx, vy;
    float steering;
    std::string name;
    float prev_x, prev_y;
    // ... (other members and methods)
};

class TrafficSimulation {
public:
    TrafficSimulation(int num_agents);
    void step(const std::vector<int>& high_level_actions, const std::vector<std::array<float, 3>>& low_level_actions);
    std::map<std::string, std::array<float, 2>> get_agent_positions();
    std::map<std::string, std::array<float, 2>> get_agent_velocities();
    std::map<std::string, std::array<float, 2>> get_previous_positions();
    // ... (other members and methods)

private:
    std::vector<Vehicle> agents;
};

#endif

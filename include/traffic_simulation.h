#ifndef TRAFFIC_SIMULATION_H
#define TRAFFIC_SIMULATION_H

#include <vector>
#include <unordered_map>
#include <string>

#include "vehicle.h"
//#include "perception_module.h"

class TrafficSimulation {
public:
    TrafficSimulation(int num_agents);
    void step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions);
    const std::vector<Vehicle>& get_agents() const;
    Vehicle& get_agent_by_name(const std::string& name);
    std::unordered_map<std::string, std::vector<float>> get_agent_positions() const;
    std::unordered_map<std::string, std::vector<float>> get_agent_velocities() const;
    std::unordered_map<std::string, std::vector<float>> get_previous_positions() const;
    // ... (other members and methods)

private:
    int num_agents;
    std::vector<Vehicle> agents;
    std::vector<Vehicle> previous_positions;

    void applyAction(int agent_idx, int high_level_action, const std::vector<float>& low_level_action);
    void updatePositions();
    void checkCollisions();
    float randFloat(float a, float b);
    // ... (other members and methods)

};

#endif

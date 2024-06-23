#ifndef TRAFFIC_SIMULATION_H
#define TRAFFIC_SIMULATION_H

#include "vehicle.h"
#include "perception_module.h"
#include <vector>
#include <unordered_map>
#include <string>

// Forward declaration of PerceptionModule
class PerceptionModule;

class TrafficSimulation {
public:
    TrafficSimulation(int num_agents);
    ~TrafficSimulation(); // Destructor to clean up perceptionModule pointer
    void step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions);
    std::unordered_map<std::string, std::vector<float>> get_agent_positions() const;
    std::unordered_map<std::string, std::vector<float>> get_agent_velocities() const;
    std::unordered_map<std::string, std::vector<float>> get_previous_positions() const;
    const std::vector<Vehicle>& getAgents() const { return agents; }
    // Method to get an agent by its name
    Vehicle& get_agent_by_name(const std::string& name);

private:
    int num_agents;
    std::vector<Vehicle> agents;
    std::vector<Vehicle> previous_positions;
    PerceptionModule* perceptionModule; // Use a pointer to PerceptionModule

    void applyAction(int agent_idx, int high_level_action, const std::vector<float>& low_level_action);
    void updatePositions();
    void checkCollisions();
    float randFloat(float a, float b);
};

#endif // TRAFFIC_SIMULATION_H

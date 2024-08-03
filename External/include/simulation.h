#ifndef SIMULATION_H
#define SIMULATION_H

#include "perception_module.h"
#include "traffic.h"

class Simulation {
public:
    Simulation(const int& num_agents, const unsigned& seed, const int& num_rays);
    ~Simulation();
    void step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions);

private:
    Traffic trafficSimulation;
    PerceptionModule perceptionModule;
};

#endif // SIMULATION_H

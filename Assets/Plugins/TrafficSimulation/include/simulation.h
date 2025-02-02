#ifndef SIMULATION_H
#define SIMULATION_H

#include "traffic.h"

class Simulation {
public:
    Simulation(const int& num_agents, const unsigned& seed);
    ~Simulation();
    void step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions);

private:
    Traffic traffic;
};

#endif // SIMULATION_H

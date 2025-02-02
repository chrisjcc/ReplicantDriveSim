#include "simulation.h"

Simulation::Simulation(const int& num_agents, const unsigned& seed)
    : traffic(num_agents, seed) {}

Simulation::~Simulation() = default;

void Simulation::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {
    traffic.step(high_level_actions, low_level_actions);
}

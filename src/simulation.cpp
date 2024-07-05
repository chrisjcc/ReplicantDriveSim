#include "simulation.h"

Simulation::Simulation(int num_agents, int num_rays)
    : trafficSimulation(num_agents), perceptionModule(trafficSimulation, num_rays) {}

Simulation::~Simulation() = default;

void Simulation::step(const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions) {
    trafficSimulation.step(high_level_actions, low_level_actions);
    perceptionModule.updatePerceptions();
}

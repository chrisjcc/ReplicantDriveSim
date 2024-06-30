#ifndef PERCEPTION_MODULE_H
#define PERCEPTION_MODULE_H

#pragma once

#include "vehicle.h"
#include "traffic_simulation.h"
#include <vector>
#include <string>

// Forward declaration of TrafficSimulation
class TrafficSimulation;

class PerceptionModule {
public:
    explicit PerceptionModule(const TrafficSimulation& sim, int num_rays = 12);
    ~PerceptionModule(); 

    void updatePerceptions();

    std::vector<float> getAgentObservation(const std::string& agent_name) const;

private:
    int numRays;
    float delta_theta;
    float rayAngleIncrement;
    const TrafficSimulation& simulation;

    float calculateDistanceToObstacle(const Vehicle& agent, float rayAngle) const;
};

#endif // PERCEPTION_MODULE_H

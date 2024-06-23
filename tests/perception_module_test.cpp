#include <gtest/gtest.h>
#include "perception_module.h"


class PerceptionModuleTest : public ::testing::Test {
protected:
    std::unique_ptr<TrafficSimulation> sim;
    std::unique_ptr<PerceptionModule> perception;

    virtual void SetUp() {
        sim = std::make_unique<TrafficSimulation>(3); // Example with 3 agents
        perception = std::make_unique<PerceptionModule>(*sim);
    }
};

TEST_F(PerceptionModuleTest, AgentNotFound) {
    std::vector<float> observations = perception->getAgentObservation("non_existing_agent");

    ASSERT_EQ(observations.size(), 12); // Assuming numRays = 12

    for (auto obs : observations) {
        EXPECT_EQ(obs, -1.0f); // Expecting default value for non-existing agent
    }
}

// Add more tests as needed for different scenarios

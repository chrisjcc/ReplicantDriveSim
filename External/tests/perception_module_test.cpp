#include <gtest/gtest.h>
#include "perception_module.h"
#include "traffic.h"

class PerceptionModuleTest : public ::testing::Test {
protected:
    std::unique_ptr<Traffic> sim;
    std::unique_ptr<PerceptionModule> perception;

    virtual void SetUp() {
        sim = std::make_unique<Traffic>(3, "external/libOpenDRIVE/test.xodr"); // Example with 3 agents
        perception = std::make_unique<PerceptionModule>(*sim);
    }
};

TEST_F(PerceptionModuleTest, AgentNotFound) {

    Vehicle* agent = new Vehicle();
    std::vector<float> observations = perception->getObservations(*agent);

    ASSERT_EQ(observations.size(), 12); // Assuming numRays = 12

    for (auto obs : observations) {
        EXPECT_EQ(obs, -1.0f); // Expecting default value for non-existing agent
    }
}

// Add more tests as needed for different scenarios

// Main function to run all tests
int main(int argc, char **argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}

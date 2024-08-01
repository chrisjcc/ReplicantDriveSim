#include <gtest/gtest.h>
#include "traffic.h"

class TrafficTest : public ::testing::Test {
protected:
    Traffic simulation;

    // Constructor: Create a Traffic object with 3 agents for testing
    TrafficTest() : simulation(3, "external/libOpenDRIVE/test.xodr") {}

    // Destructor
    virtual ~TrafficTest() {}

    // Set up function to be executed before each test case
    virtual void SetUp() {}

    // Tear down function to be executed after each test case
    virtual void TearDown() {}
};

// Test case to verify basic movement and action application
TEST_F(TrafficTest, BasicMovementAndAction) {
    // Perform actions and verify positions and velocities

    // Get initial positions and velocities
    auto initial_positions = simulation.get_agent_positions();
    auto initial_velocities = simulation.get_agent_velocities();

    // Example actions
    std::vector<int> high_level_actions = {0, 1, 2};
    std::vector<std::vector<float>> low_level_actions = {{0.1f, 1.0f, 0.0f}, {-0.2f, 0.0f, 0.0f}, {0.0f, 0.0f, 1.0f}};

    // Step simulation with actions
    simulation.step(high_level_actions, low_level_actions);

    // Get updated positions and velocities
    auto updated_positions = simulation.get_agent_positions();
    auto updated_velocities = simulation.get_agent_velocities();

    // Verify that positions and velocities have changed appropriately
    const float epsilon = 1e-5f; // Small tolerance for floating-point comparisons

    for (int i = 0; i < 3; ++i) {
        auto initial_pos = initial_positions["agent_" + std::to_string(i)];
        auto updated_pos = updated_positions["agent_" + std::to_string(i)];
        EXPECT_GT((updated_pos[0] - initial_pos[0])*(updated_pos[0] - initial_pos[0]) +
                  (updated_pos[1] - initial_pos[1])*(updated_pos[1] - initial_pos[1]), epsilon);

        auto initial_vel = initial_velocities["agent_" + std::to_string(i)];
        auto updated_vel = updated_velocities["agent_" + std::to_string(i)];
        EXPECT_GT(std::abs(updated_vel[0] - initial_vel[0]) +
                  std::abs(updated_vel[1] - initial_vel[1]), epsilon);
    }
}

// Test case to verify collision detection
TEST_F(TrafficTest, CollisionDetection) {
    // Setup initial positions to induce collision
    simulation.get_agent_by_name("agent_0").setX(50.0f);
    simulation.get_agent_by_name("agent_1").setX(55.0f);
    simulation.get_agent_by_name("agent_0").setY(100.0f);
    simulation.get_agent_by_name("agent_1").setY(100.0f);

    // Step simulation
    simulation.step({0, 0, 0}, {{0.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 0.0f}, {0.0f, 0.0f, 0.0f}});

    // Verify collision handling
    auto positions_after_collision = simulation.get_agent_positions();
    auto velocities_after_collision = simulation.get_agent_velocities();

    // Agents 0 and 1 should have velocities set to 0 after collision
    const float epsilon = 1e-5f; // Small tolerance for floating-point comparisons
    EXPECT_NEAR(velocities_after_collision["agent_0"][0], 0.0f, epsilon);
    EXPECT_NEAR(velocities_after_collision["agent_0"][1], 0.0f, epsilon);
    EXPECT_NEAR(velocities_after_collision["agent_1"][0], 0.0f, epsilon);
    EXPECT_NEAR(velocities_after_collision["agent_1"][1], 0.0f, epsilon);
}

// Main function to run all tests
int main(int argc, char **argv) {
    ::testing::InitGoogleTest(&argc, argv);
    return RUN_ALL_TESTS();
}

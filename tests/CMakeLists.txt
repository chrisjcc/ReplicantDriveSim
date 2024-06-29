# Minimum required version of CMake
cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(TrafficSimulationTests)

# Source files for tests
set(TEST_SOURCES
    traffic_simulation_test.cpp
)

# Create the test executable
add_executable(traffic_simulation_test ${TEST_SOURCES})

# Link the test executable with Google Test and the traffic simulation static library
target_link_libraries(traffic_simulation_test gtest traffic_simulation_static)

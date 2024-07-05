# Minimum required version of CMake
cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(TrafficSimulationTests)

# Source files for tests
set(TEST_SOURCES
    traffic_test.cpp
    perception_module_test.cpp
)

# Create the test executable
add_executable(traffic_test traffic_test.cpp)
add_executable(perception_module_test perception_module_test.cpp)

# Link the test executables with Google Test, gtest_main, and the traffic simulation static library
target_link_libraries(traffic_test gtest simulation_static)
target_link_libraries(perception_module_test gtest simulation_static)

# Optionally, specify a C++ standard
set_target_properties(perception_module_test PROPERTIES
                      CXX_STANDARD 14
                      CXX_STANDARD_REQUIRED YES
                      )

set_target_properties(traffic_test PROPERTIES
                      CXX_STANDARD 14
                      CXX_STANDARD_REQUIRED YES
                      )

add_executable(perception_module_test perception_module_test.cpp)
add_executable(traffic_simulation_test traffic_simulation_test.cpp)

# Specify include directories for the test executable
target_include_directories(perception_module_test PRIVATE
    ${PROJECT_SOURCE_DIR}/lib/gtest/include
    ${PROJECT_SOURCE_DIR}/include
)

# Specify include directories for the test executable
target_include_directories(traffic_simulation_test PRIVATE
    ${PROJECT_SOURCE_DIR}/lib/gtest/include
    ${PROJECT_SOURCE_DIR}/include
)

# Link Google Test libraries
target_link_libraries(perception_module_test
    traffic_simulation_lib
    ${PROJECT_SOURCE_DIR}/lib/gtest/libgtest.a
    ${PROJECT_SOURCE_DIR}/lib/gtest/libgtest_main.a
)

# Link Google Test libraries and your source libraries
target_link_libraries(traffic_simulation_test
    ${PROJECT_SOURCE_DIR}/lib/gtest/libgtest.a
    ${PROJECT_SOURCE_DIR}/lib/gtest/libgtest_main.a
    traffic_simulation_lib
    # Add other libraries if needed
)

# Link your source files (libraries)
target_link_libraries(perception_module_test
    perception_module
)

# Optionally, specify a C++ standard
set_target_properties(perception_module_test PROPERTIES
                      CXX_STANDARD 14
                      CXX_STANDARD_REQUIRED YES
                      )

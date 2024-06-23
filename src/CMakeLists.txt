cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(traffic_simulation)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)

message(STATUS "CMAKE_BINARY_DIR is ${CMAKE_BINARY_DIR}")

# Set the output directory for the library
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

message(STATUS "CMAKE_LIBRARY_OUTPUT_DIRECTORY is ${CMAKE_LIBRARY_OUTPUT_DIRECTORY}")

# Set the policy to find Python automatically
set(PYBIND11_FINDPYTHON ON)

# Get the pybind11 directory dynamically
execute_process(
    COMMAND python3 -m pybind11 --cmakedir
    OUTPUT_VARIABLE pybind11_DIR
    OUTPUT_STRIP_TRAILING_WHITESPACE
)

# Set the pybind11_DIR variable
set(pybind11_DIR ${pybind11_DIR})

message(STATUS "pybind11_DIR is ${pybind11_DIR}")

# Find pybind11
find_package(pybind11 REQUIRED PATHS ${pybind11_DIR})

# Check if pybind11 was found
if (NOT pybind11_FOUND)
    message(FATAL_ERROR "pybind11 not found")
endif()

message(STATUS "CMAKE_CURRENT_SOURCE_DIR is ${CMAKE_CURRENT_SOURCE_DIR}")

# Define source files
set(SOURCE_FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/traffic_simulation.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/perception_module.cpp
)

message(STATUS "SOURCE_FILES is ${SOURCE_FILES}")

# Create a static or shared library
add_library(perception_module OBJECT perception_module.cpp)  # Object library if used by multiple targets
add_library(traffic_simulation_lib STATIC ${SOURCE_FILES})

message(STATUS "Include directory is ${CMAKE_CURRENT_SOURCE_DIR}/../include")

# Include traffic_simulation headers
target_include_directories(traffic_simulation_lib PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/../include)

# Add compile options
target_compile_options(traffic_simulation_lib PRIVATE -Wall -Wextra -Werror)

# Add the pybind11 module (Python bindings)
set(PYBIND_SOURCE_FILES ${CMAKE_CURRENT_SOURCE_DIR}/bindings.cpp)
pybind11_add_module(traffic_simulation ${PYBIND_SOURCE_FILES})

# Link the core library to the pybind11 module
target_link_libraries(traffic_simulation PRIVATE traffic_simulation_lib)

# Set the output directory for the library
set_target_properties(traffic_simulation PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/src
    PREFIX ""
)

# Explicitly specify where to place the generated shared object file
set_target_properties(traffic_simulation PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
)

# Install the library
install(TARGETS traffic_simulation
        LIBRARY DESTINATION ${CMAKE_INSTALL_PREFIX}/traffic_simulation)

# Install the headers
install(FILES ${CMAKE_CURRENT_SOURCE_DIR}/../include/traffic_simulation.h
        DESTINATION ${CMAKE_INSTALL_PREFIX}/include)

# Copy headers to the build/include directory (if needed for development)
add_custom_command(TARGET traffic_simulation_lib POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
    ${CMAKE_CURRENT_SOURCE_DIR}/../include
    ${CMAKE_BINARY_DIR}/include)

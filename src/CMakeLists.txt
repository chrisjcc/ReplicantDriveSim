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

# Source files for the traffic simulation
set(SOURCE_FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/traffic_simulation.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/bindings.cpp
)

message(STATUS "SOURCE_FILES is ${SOURCE_FILES}")

# Create a static library for the traffic simulation
add_library(traffic_simulation_static STATIC ${SOURCE_FILES})

# Add the pybind11 module
pybind11_add_module(traffic_simulation ${SOURCE_FILES})

# Set the output directory for the pybind11 module
set_target_properties(traffic_simulation PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/src
    PREFIX ""
)

message(STATUS "Include directory is ${CMAKE_CURRENT_SOURCE_DIR}/../include")

# Specify include directories
target_include_directories(traffic_simulation_static PUBLIC ${CMAKE_SOURCE_DIR}/include ${pybind11_INCLUDE_DIRS})
target_include_directories(traffic_simulation PUBLIC ${CMAKE_SOURCE_DIR}/include)

# Add compile options
target_compile_options(traffic_simulation_static PRIVATE -Wall -Wextra -Werror)
target_compile_options(traffic_simulation PRIVATE -Wall -Wextra -Werror)

# Explicitly specify where to place the generated shared object file
set_target_properties(traffic_simulation_static PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
)

# Install the static library
install(TARGETS traffic_simulation_static
        LIBRARY DESTINATION ${CMAKE_INSTALL_PREFIX}/traffic_simulation)

# Install the headers
install(FILES ${CMAKE_CURRENT_SOURCE_DIR}/../include/traffic_simulation.h
        DESTINATION ${CMAKE_INSTALL_PREFIX}/include)

# Copy headers to the build/include directory (if needed for development)
add_custom_command(TARGET traffic_simulation_static POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
    ${CMAKE_SOURCE_DIR}/include
    ${CMAKE_BINARY_DIR}/include)

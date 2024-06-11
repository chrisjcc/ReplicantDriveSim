cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(traffic_simulation)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)

message(STATUS "pybind11_DIR is ${pybind11_DIR}")

# Add this line before pybind11 to set the policy
set(PYBIND11_FINDPYTHON ON)

# Get the pybind11 directory dynamically
execute_process(
    COMMAND python3 -m pybind11 --cmakedir
    OUTPUT_VARIABLE pybind11_DIR
    OUTPUT_STRIP_TRAILING_WHITESPACE
)

# Set the pybind11_DIR variable
set(pybind11_DIR ${pybind11_DIR})

find_package(pybind11 REQUIRED PATHS ${pybind11_DIR})

message(STATUS "CMAKE_CURRENT_SOURCE_DIR is ${CMAKE_CURRENT_SOURCE_DIR}")

# Define source files
set(SOURCE_FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/traffic_simulation.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/bindings.cpp
)

message(STATUS "SOURCE_FILES is ${SOURCE_FILES}")

# Add library target
add_library(traffic_simulation MODULE ${SOURCE_FILES})

# Remove the "lib" prefix
set_target_properties(traffic_simulation PROPERTIES PREFIX "")

message(STATUS "CMAKE_BINARY_DIR is ${CMAKE_BINARY_DIR}")

# Set the output directory for the library
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/src)

message(STATUS "Include directory is ${CMAKE_CURRENT_SOURCE_DIR}/../include")

# Include traffic_simulation hearders
target_include_directories(traffic_simulation PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/../include)

# Link pybind11 module
target_link_libraries(traffic_simulation PRIVATE pybind11::module)

target_compile_options(traffic_simulation PRIVATE -Wall -Wextra -Werror)

message(STATUS "CMAKE_INSTALL_PREFIX is ${CMAKE_INSTALL_PREFIX}")

# Install the library
install(TARGETS traffic_simulation
        LIBRARY DESTINATION ${CMAKE_INSTALL_PREFIX}/traffic_simulation)

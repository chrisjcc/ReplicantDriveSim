cmake_minimum_required(VERSION 3.4...3.18)
project(traffic_simulation)

set(CMAKE_CXX_STANDARD 14)

# Get the pybind11 directory dynamically
execute_process(
    COMMAND python3 -m pybind11 --cmakedir
    OUTPUT_VARIABLE pybind11_DIR
    OUTPUT_STRIP_TRAILING_WHITESPACE
)

find_package(pybind11 REQUIRED PATHS ${pybind11_DIR})
find_package(pybind11 REQUIRED)

pybind11_add_module(traffic_simulation bindings.cpp)

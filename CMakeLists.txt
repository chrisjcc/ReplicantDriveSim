cmake_minimum_required(VERSION 3.4...3.18)
project(traffic_simulation)

set(CMAKE_CXX_STANDARD 14)

# Add this line before pybind11 to set the policy
set(PYBIND11_FINDPYTHON ON)

# Get the pybind11 directory dynamically
execute_process(
    COMMAND python3 -m pybind11 --cmakedir
    OUTPUT_VARIABLE pybind11_DIR
    OUTPUT_STRIP_TRAILING_WHITESPACE
)

find_package(pybind11 REQUIRED PATHS ${pybind11_DIR})

add_library(traffic_simulation MODULE
    traffic_simulation.cpp
    bindings.cpp  # Ensure bindings.cpp is included
)

target_include_directories(traffic_simulation PRIVATE ${PYBIND11_INCLUDE_DIRS})
target_link_libraries(traffic_simulation PRIVATE pybind11::module)

target_compile_options(traffic_simulation PRIVATE -Wall -Wextra -Werror)
# Remove the problematic linker option
# target_link_options(traffic_simulation PRIVATE -Wl,--no-undefined)

# Set properties to ensure the output is a Python module
set_target_properties(traffic_simulation PROPERTIES PREFIX "")
set_target_properties(traffic_simulation PROPERTIES SUFFIX ".so")

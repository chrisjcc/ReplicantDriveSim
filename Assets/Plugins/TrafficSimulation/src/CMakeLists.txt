cmake_minimum_required(VERSION 3.4...3.18)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)

message(STATUS "CMAKE_BINARY_DIR is ${CMAKE_BINARY_DIR}")

# Set the output directory for the library
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR})

message(STATUS "CMAKE_LIBRARY_OUTPUT_DIRECTORY is ${CMAKE_LIBRARY_OUTPUT_DIRECTORY}")

# Define the target name
set(REPLICANT_DRIVE_SIM_TARGET libReplicantDriveSim)

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
    ${CMAKE_CURRENT_SOURCE_DIR}/simulation.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/traffic.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/vehicle.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/bindings.cpp)

message(STATUS "SOURCE_FILES is ${SOURCE_FILES}")

# Create a static library for the traffic simulation
add_library(${REPLICANT_DRIVE_SIM_TARGET}_static STATIC ${SOURCE_FILES})

# Add the pybind11 module
pybind11_add_module(${REPLICANT_DRIVE_SIM_TARGET} ${SOURCE_FILES})

# Set properties for the pybind11 module
set_target_properties(${REPLICANT_DRIVE_SIM_TARGET} PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
    PREFIX ""
)

# Override the default .dylib extension with .so on macOS
if(APPLE)
    set_target_properties(${REPLICANT_DRIVE_SIM_TARGET} PROPERTIES SUFFIX ".so")
endif()

message(STATUS "Include directory is ${CMAKE_CURRENT_SOURCE_DIR}/../include")

# Specify include directories
target_include_directories(${REPLICANT_DRIVE_SIM_TARGET}_static PUBLIC ${CMAKE_SOURCE_DIR}/include ${pybind11_INCLUDE_DIRS})
target_include_directories(${REPLICANT_DRIVE_SIM_TARGET} PUBLIC ${CMAKE_SOURCE_DIR}/include)

# Add compile options
target_compile_options(${REPLICANT_DRIVE_SIM_TARGET}_static PRIVATE -Wall -Wextra)
target_compile_options(${REPLICANT_DRIVE_SIM_TARGET} PRIVATE -Wall -Wextra)

# Explicitly specify where to place the generated shared object file
set_target_properties(${REPLICANT_DRIVE_SIM_TARGET}_static PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin
    LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
    ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
)

# Install the static library
install(TARGETS ${REPLICANT_DRIVE_SIM_TARGET}_static
        LIBRARY DESTINATION ${CMAKE_INSTALL_PREFIX}/${REPLICANT_DRIVE_SIM_TARGET})

# Install the headers
install(FILES ${CMAKE_CURRENT_SOURCE_DIR}/../include/simulation.h
        DESTINATION ${CMAKE_INSTALL_PREFIX}/include)

# Copy headers to the build/include directory (if needed for development)
#add_custom_command(TARGET ${REPLICANT_DRIVE_SIM_TARGET}_static POST_BUILD
#    COMMAND ${CMAKE_COMMAND} -E copy_directory
#    ${CMAKE_SOURCE_DIR}/include
#    ${CMAKE_BINARY_DIR}/include)

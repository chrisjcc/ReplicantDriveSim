cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(ReplicantDriveSim)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)

# Include directories
include_directories(include)

# Include subdirectories
add_subdirectory(src)
add_subdirectory(tests)

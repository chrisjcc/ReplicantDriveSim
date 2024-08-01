cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(ReplicantDriveSim)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)

# Include directories
include_directories(${CMAKE_SOURCE_DIR}/include)
include_directories(${CMAKE_SOURCE_DIR}/external/libOpenDRIVE/include)
include_directories(${CMAKE_SOURCE_DIR}/external/libOpenDRIVE/thirdparty)

# Add Google Test subdirectory
add_subdirectory(${CMAKE_SOURCE_DIR}/external/googletest)

# Add the libOpenDRIVE subdirectory
add_subdirectory(${CMAKE_SOURCE_DIR}/external/libOpenDRIVE)

# Include subdirectories
add_subdirectory(${CMAKE_SOURCE_DIR}/src)
add_subdirectory(${CMAKE_SOURCE_DIR}/tests)

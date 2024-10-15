cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(ReplicantDriveSim)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED True)

# Set compiler flags
set(CMAKE_CXX_FLAGS "-Wall")
set(CMAKE_CXX_FLAGS_DEBUG "-g")

# Define VERSION_INFO for the build
# VERSION_INFO must be passed through CXXFLAGS from the setup.py
if (VERSION_INFO)
    add_definitions(-DVERSION_INFO="${VERSION_INFO}")
endif()

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

cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(ReplicantDriveSim VERSION 1.0.0 LANGUAGES CXX)

# Define target names
set(UNITY_TARGET libReplicantDriveSim)
set(PYPI_TARGET replicantdrivesim)

# Define VERSION_INFO for PyPI build
if (VERSION_INFO)
    add_definitions(-DVERSION_INFO="${VERSION_INFO}")
endif()

# Set C++ standard and compiler flags
set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall")
set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} -g")

message(STATUS "CMAKE_SOURCE_DIR is: ${CMAKE_SOURCE_DIR}")

# -------- UNITY TARGET CONFIGURATION --------

# Define source and header files for Unity
set(UNITY_SOURCES
    ${CMAKE_SOURCE_DIR}/src/traffic.cpp
    ${CMAKE_SOURCE_DIR}/src/vehicle.cpp
    ${CMAKE_SOURCE_DIR}/src/simulation.cpp
    ${CMAKE_SOURCE_DIR}/src/bicycle_model.cpp
    ${CMAKE_SOURCE_DIR}/src/traffic_simulation_c_api.cpp
)

set(UNITY_HEADERS
    ${CMAKE_SOURCE_DIR}/include/traffic.h
    ${CMAKE_SOURCE_DIR}/include/vehicle.h
    ${CMAKE_SOURCE_DIR}/include/simulation.h
    ${CMAKE_SOURCE_DIR}/include/bicycle_model.h
    ${CMAKE_SOURCE_DIR}/include/traffic_simulation_c_api.h

)

# Create build directory for Unity target
set(UNITY_BUILD_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/build")
file(MAKE_DIRECTORY ${UNITY_BUILD_DIRECTORY})

# Create shared library for Unity
add_library(${UNITY_TARGET} SHARED ${UNITY_SOURCES})

# Set properties for Unity library
set_target_properties(${UNITY_TARGET} PROPERTIES
    PREFIX ""  # To avoid prepending "lib" to target libary e.g. libReplicantDriveSim.dylib
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}"
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
    VERSION ${PROJECT_VERSION}
    SOVERSION ${PROJECT_VERSION_MAJOR}
)

# Platform-specific settings for Unity
if(WIN32)
    add_definitions(-DBUILDING_DLL)
    set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)
else()
    add_definitions(-DEXPORT=__attribute__\(\(visibility\(\"default\"\)\)\))
    set(CMAKE_CXX_VISIBILITY_PRESET hidden)
    set(CMAKE_VISIBILITY_INLINES_HIDDEN 1)
endif()

# MacOS-specific settings for Unity
if(APPLE)
    set_target_properties(${UNITY_TARGET} PROPERTIES
        MACOSX_RPATH ON
        INSTALL_NAME_DIR "@rpath"
    )
endif()

# Add include directories for Unity
target_include_directories(${UNITY_TARGET} PUBLIC ${CMAKE_SOURCE_DIR}/include)

# -------- PYPI TARGET CONFIGURATION --------

# Source files for the PyPI target
set(PYPI_SOURCE_FILES
    ${CMAKE_SOURCE_DIR}/src/simulation.cpp
    ${CMAKE_SOURCE_DIR}/src/traffic.cpp
    ${CMAKE_SOURCE_DIR}/src/vehicle.cpp
    ${CMAKE_SOURCE_DIR}/src/bindings.cpp
    ${CMAKE_SOURCE_DIR}/src/bicycle_model.cpp
)

# Set the output directory for the PyPI target
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR})

# Enable automatic Python detection for pybind11
set(PYBIND11_FINDPYTHON ON)

# Find Python interpreter and development libraries
find_package(Python3 COMPONENTS Interpreter Development REQUIRED)

# Find pybind11 for PyPI target
find_package(pybind11 CONFIG QUIET)
if(NOT pybind11_FOUND)
    # If pybind11 not found, use Python to get the CMake directory
    execute_process(
        COMMAND "${Python3_EXECUTABLE}" -c "import pybind11; print(pybind11.get_cmake_dir())"
        OUTPUT_VARIABLE PYBIND11_CMAKE_DIR
        OUTPUT_STRIP_TRAILING_WHITESPACE
        RESULT_VARIABLE PYBIND11_IMPORT_RESULT
    )
    if(PYBIND11_IMPORT_RESULT EQUAL 0)
        set(pybind11_DIR "${PYBIND11_CMAKE_DIR}")
        find_package(pybind11 CONFIG REQUIRED)
    else()
        message(FATAL_ERROR "Failed to find pybind11. Please install it or set pybind11_DIR manually.")
    endif()
endif()

message(STATUS "Found pybind11 v${pybind11_VERSION}: ${pybind11_INCLUDE_DIRS}")

# Add the pybind11 module for PyPI
pybind11_add_module(${PYPI_TARGET} ${PYPI_SOURCE_FILES})

# Set properties for the PyPI target
set_target_properties(${PYPI_TARGET} PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}"
    PREFIX ""
    SUFFIX ".so"  # Explicitly set .so suffix
)

# MacOS-specific settings for PyPI
if(APPLE)
    set_target_properties(${PYPI_TARGET} PROPERTIES SUFFIX ".so")
endif()

# Include directories for PyPI
target_include_directories(${PYPI_TARGET} PUBLIC
    ${CMAKE_SOURCE_DIR}/include
    ${pybind11_INCLUDE_DIRS}
)

# Add compile options for PyPI
target_compile_options(${PYPI_TARGET} PRIVATE -Wall -Wextra)

# Install commands for PyPI
install(TARGETS ${PYPI_TARGET}
        LIBRARY DESTINATION ${CMAKE_INSTALL_PREFIX}/${PYPI_TARGET})

install(FILES ${CMAKE_SOURCE_DIR}/include/simulation.h
        DESTINATION ${CMAKE_INSTALL_PREFIX}/include)

# Install Unity files (like the macOS .app)
install(DIRECTORY ${CMAKE_SOURCE_DIR}/Builds/macOS
        DESTINATION ${CMAKE_INSTALL_PREFIX}/Builds/StandaloneOSX)

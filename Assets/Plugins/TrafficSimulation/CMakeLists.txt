cmake_minimum_required(VERSION 3.4...3.18)

# Set project name
project(ReplicantDriveSim VERSION 1.0.0 LANGUAGES CXX)

# Set C++ standard
set(CMAKE_CXX_STANDARD 14)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Set compiler flags
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -Wall")
set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} -g")

# Define VERSION_INFO for the build
# VERSION_INFO must be passed through CXXFLAGS from the setup.py
if (VERSION_INFO)
    add_definitions(-DVERSION_INFO="${VERSION_INFO}")
endif()

# Define EXPORT macro
if(WIN32)
    add_definitions(-DBUILDING_DLL)
    set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS ON)
else()
    add_definitions(-DEXPORT=__attribute__\(\(visibility\(\"default\"\)\)\))
    set(CMAKE_CXX_VISIBILITY_PRESET hidden)
    set(CMAKE_VISIBILITY_INLINES_HIDDEN 1)
endif()

# Set the policy to find Python automatically
set(PYBIND11_FINDPYTHON ON)

# Find Python3 development libraries
find_package(Python3 REQUIRED COMPONENTS Interpreter Development)

if(NOT DEFINED pybind11_DIR)
    execute_process(
        COMMAND ${Python3_EXECUTABLE} -c "import pybind11; print(pybind11.get_cmake_dir())"
        OUTPUT_VARIABLE PYBIND11_CMAKE_DIR
        OUTPUT_STRIP_TRAILING_WHITESPACE
    )
    set(pybind11_DIR ${PYBIND11_CMAKE_DIR})
endif()

# Find pybind11
find_package(pybind11 CONFIG REQUIRED)
if (NOT pybind11_FOUND)
    message(FATAL_ERROR "pybind11 not found. Please install pybind11 or set pybind11_DIR.")
endif()

message(STATUS "pybind11_DIR is ${pybind11_DIR}")

# Include directories
include_directories(${CMAKE_CURRENT_SOURCE_DIR}/include ${pybind11_INCLUDE_DIRS})

# Add source files
file(GLOB_RECURSE SOURCES "src/*.cpp")
file(GLOB_RECURSE HEADERS "include/*.h")

# Create shared library
add_library(simulation SHARED ${SOURCES} ${HEADERS})

# Link against pybind11 and Python
target_link_libraries(simulation PRIVATE pybind11::module Python3::Python)

# Set the build directory relative to the current source directory
#set(BUILD_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/build")

# Create the build directory if it does not exist
#file(MAKE_DIRECTORY ${BUILD_DIRECTORY})

# Use the CMAKE_LIBRARY_OUTPUT_DIRECTORY if it's set
if(NOT CMAKE_LIBRARY_OUTPUT_DIRECTORY)
    set(CMAKE_LIBRARY_OUTPUT_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}")
endif()


# Set output directory for the shared library
set_target_properties(simulation PROPERTIES
    #LIBRARY_OUTPUT_DIRECTORY "${BUILD_DIRECTORY}"
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_LIBRARY_OUTPUT_DIRECTORY}"
    VERSION ${PROJECT_VERSION}
    SOVERSION ${PROJECT_VERSION_MAJOR}
)

# MacOS specific settings
if(APPLE)
    set_target_properties(simulation PROPERTIES
        #SUFFIX ".so" # to override the default extension .dylib for shared libraries on macOS.
        MACOSX_RPATH ON
        INSTALL_NAME_DIR "@rpath"
    )

    # Find and link against Python framework
    find_library(PYTHON_LIBRARY NAMES Python PATHS ${Python3_LIBRARY_DIRS})
    if(PYTHON_LIBRARY)
        target_link_libraries(simulation PRIVATE ${PYTHON_LIBRARY})
    endif()
endif()

#!/bin/bash

# ReplicantDriveSim Native Library Build Script
# This script builds the C++ traffic simulation library for Unity

set -e  # Exit on error

echo "=================================================="
echo "ReplicantDriveSim - Native Library Build Script"
echo "=================================================="

# Detect platform
OS="$(uname -s)"
case "${OS}" in
    Linux*)     PLATFORM=Linux;;
    Darwin*)    PLATFORM=macOS;;
    CYGWIN*|MINGW*|MSYS*) PLATFORM=Windows;;
    *)          PLATFORM="UNKNOWN:${OS}"
esac

echo "Platform detected: ${PLATFORM}"

# Check for CMake
if ! command -v cmake &> /dev/null; then
    echo "ERROR: CMake is not installed!"
    echo ""
    if [ "$PLATFORM" = "macOS" ]; then
        echo "Install CMake with: brew install cmake"
    elif [ "$PLATFORM" = "Linux" ]; then
        echo "Install CMake with: sudo apt-get install cmake"
    fi
    exit 1
fi

echo "CMake version: $(cmake --version | head -n1)"

# Navigate to plugin directory
PLUGIN_DIR="Assets/Plugins/TrafficSimulation"
if [ ! -d "$PLUGIN_DIR" ]; then
    echo "ERROR: Plugin directory not found: $PLUGIN_DIR"
    echo "Are you running this script from the ReplicantDriveSim root directory?"
    exit 1
fi

cd "$PLUGIN_DIR"
echo "Working directory: $(pwd)"

# Create and enter build directory
echo ""
echo "Creating build directory..."
mkdir -p build
cd build

# Platform-specific configuration
if [ "$PLATFORM" = "macOS" ]; then
    # Set macOS deployment target for compatibility
    export MACOSX_DEPLOYMENT_TARGET=14.0
    echo "macOS Deployment Target: $MACOSX_DEPLOYMENT_TARGET"

    # Detect CPU architecture
    ARCH="$(uname -m)"
    echo "Architecture: $ARCH"

    if [ "$ARCH" = "arm64" ]; then
        echo "Building for Apple Silicon (ARM64)..."
        CMAKE_ARGS="-DCMAKE_OSX_ARCHITECTURES=arm64"
    else
        echo "Building for Intel (x86_64)..."
        CMAKE_ARGS="-DCMAKE_OSX_ARCHITECTURES=x86_64"
    fi
fi

# Configure with CMake
echo ""
echo "Configuring with CMake..."
cmake ${CMAKE_ARGS:-} ..

# Determine number of CPU cores
if [ "$PLATFORM" = "macOS" ]; then
    CORES=$(sysctl -n hw.ncpu)
elif [ "$PLATFORM" = "Linux" ]; then
    CORES=$(nproc)
else
    CORES=4  # Default fallback
fi

echo "Building with $CORES parallel jobs..."

# Build the library
echo ""
echo "Building the library..."
make -j${CORES}

# Check build result
if [ $? -eq 0 ]; then
    echo ""
    echo "=================================================="
    echo "Build successful!"
    echo "=================================================="

    # Find the built library
    if [ "$PLATFORM" = "macOS" ]; then
        LIBRARY_FILE=$(find . -name "libReplicantDriveSim.dylib" -o -name "libReplicantDriveSim.bundle" | head -n1)
        EXTENSION=".dylib"
    elif [ "$PLATFORM" = "Linux" ]; then
        LIBRARY_FILE=$(find . -name "libReplicantDriveSim.so" | head -n1)
        EXTENSION=".so"
    else
        LIBRARY_FILE=$(find . -name "ReplicantDriveSim.dll" | head -n1)
        EXTENSION=".dll"
    fi

    if [ -n "$LIBRARY_FILE" ]; then
        echo "Built library: $LIBRARY_FILE"
        echo "Library size: $(du -h "$LIBRARY_FILE" | cut -f1)"

        # Copy to parent directory (Unity Plugins folder)
        DEST_DIR=".."
        echo ""
        echo "Copying library to Unity Plugins folder..."
        cp -v "$LIBRARY_FILE" "$DEST_DIR/"

        echo ""
        echo "=================================================="
        echo "✅ Setup Complete!"
        echo "=================================================="
        echo ""
        echo "Next steps:"
        echo "1. Open the project in Unity 6"
        echo "2. Select the library file: Assets/Plugins/TrafficSimulation/libReplicantDriveSim${EXTENSION}"
        echo "3. In Inspector, configure:"
        echo "   - Select platforms: ${PLATFORM}"
        if [ "$PLATFORM" = "macOS" ]; then
            echo "   - CPU: ${ARCH}"
        fi
        echo "   - Load on startup: ✓"
        echo "4. Click 'Apply'"
        echo "5. Press Play to test!"
        echo ""
    else
        echo "WARNING: Could not find built library file"
        echo "Check the build directory manually: $(pwd)"
        exit 1
    fi
else
    echo ""
    echo "=================================================="
    echo "❌ Build failed!"
    echo "=================================================="
    echo "Check the error messages above for details."
    exit 1
fi

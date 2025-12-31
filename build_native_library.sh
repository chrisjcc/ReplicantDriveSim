#!/bin/bash

# ReplicantDriveSim Native Library Build Script - Fixed Version
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

# Clean previous build
echo ""
echo "Cleaning previous build..."
rm -rf build
rm -f libReplicantDriveSim.dylib libReplicantDriveSim.so ReplicantDriveSim.dll

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
make -j${CORES} VERBOSE=1

# Check build result
if [ $? -eq 0 ]; then
    echo ""
    echo "=================================================="
    echo "Build successful!"
    echo "=================================================="

    # Find the built library (actual file, not symlink)
    if [ "$PLATFORM" = "macOS" ]; then
        # Find the actual versioned library file (not symlinks)
        LIBRARY_FILE=$(find . -name "libReplicantDriveSim.*.*.*.dylib" -type f | head -n1)
        if [ -z "$LIBRARY_FILE" ]; then
            # Fallback to finding any .dylib (might be symlink)
            LIBRARY_FILE=$(find . -name "libReplicantDriveSim.dylib" | head -n1)
        fi
        EXTENSION=".dylib"
    elif [ "$PLATFORM" = "Linux" ]; then
        # Find the actual versioned library file
        LIBRARY_FILE=$(find . -name "libReplicantDriveSim.so.*" -type f | head -n1)
        if [ -z "$LIBRARY_FILE" ]; then
            LIBRARY_FILE=$(find . -name "libReplicantDriveSim.so" | head -n1)
        fi
        EXTENSION=".so"
    else
        LIBRARY_FILE=$(find . -name "ReplicantDriveSim.dll" | head -n1)
        EXTENSION=".dll"
    fi

    if [ -n "$LIBRARY_FILE" ] && [ -f "$LIBRARY_FILE" ]; then
        echo ""
        echo "Found library: $LIBRARY_FILE"

        # Check file size BEFORE moving
        FILE_SIZE=$(stat -f%z "$LIBRARY_FILE" 2>/dev/null || stat -c%s "$LIBRARY_FILE" 2>/dev/null || echo "0")
        HUMAN_SIZE=$(du -h "$LIBRARY_FILE" | cut -f1)

        echo "Library size: $HUMAN_SIZE ($FILE_SIZE bytes)"

        if [ "$FILE_SIZE" -eq 0 ]; then
            echo ""
            echo "=================================================="
            echo "❌ ERROR: Library file is 0 bytes!"
            echo "=================================================="
            echo "The build produced an empty library file."
            echo "This usually means:"
            echo "1. Linking failed but didn't report an error"
            echo "2. CMake configuration issue"
            echo ""
            echo "Check the build output above for linker warnings."
            exit 1
        fi

        # Verify library has symbols
        if [ "$PLATFORM" = "macOS" ]; then
            echo ""
            echo "Verifying library contents..."
            if ! otool -L "$LIBRARY_FILE" > /dev/null 2>&1; then
                echo "WARNING: Library might be corrupted (otool failed)"
            fi

            # Check for expected symbols
            if nm "$LIBRARY_FILE" 2>/dev/null | grep -q "Traffic_create"; then
                echo "✓ Found expected symbol: Traffic_create"
            else
                echo "❌ WARNING: Expected symbol 'Traffic_create' not found!"
                echo "Available symbols:"
                nm "$LIBRARY_FILE" 2>/dev/null | grep " T " | head -10
            fi
        fi

        # Copy to parent directory (Unity Plugins folder)
        # We use cp (not mv) for the actual file to preserve the original
        # Unity needs the unversioned name
        DEST_DIR=".."
        DEST_FILE="$DEST_DIR/libReplicantDriveSim${EXTENSION}"

        echo ""
        echo "Copying library to Unity Plugins folder..."
        # If source is a symlink, follow it and copy the actual file
        if [ -L "$LIBRARY_FILE" ]; then
            echo "Note: Source is a symlink, copying target file..."
            cp -fL "$LIBRARY_FILE" "$DEST_FILE"
        else
            echo "Copying actual library file..."
            cp -f "$LIBRARY_FILE" "$DEST_FILE"
        fi

        # Verify the moved file
        if [ -f "$DEST_FILE" ]; then
            MOVED_SIZE=$(stat -f%z "$DEST_FILE" 2>/dev/null || stat -c%s "$DEST_FILE" 2>/dev/null || echo "0")
            echo "✓ Library successfully moved"
            echo "Final location: $DEST_FILE"
            echo "Final size: $(du -h "$DEST_FILE" | cut -f1) ($MOVED_SIZE bytes)"

            if [ "$MOVED_SIZE" -eq 0 ]; then
                echo ""
                echo "❌ ERROR: Moved file is 0 bytes!"
                exit 1
            fi
        else
            echo "❌ ERROR: Failed to move library to $DEST_FILE"
            exit 1
        fi

        echo ""
        echo "=================================================="
        echo "✅ Setup Complete!"
        echo "=================================================="
        echo ""
        echo "Next steps:"
        echo "1. In Unity, select: Assets/Plugins/TrafficSimulation/libReplicantDriveSim${EXTENSION}"
        echo "2. In Inspector, configure:"
        echo "   - Select platforms: ${PLATFORM}"
        if [ "$PLATFORM" = "macOS" ]; then
            echo "   - CPU: ${ARCH}"
        fi
        echo "   - Load on startup: ✓"
        echo "3. Click 'Apply'"
        echo "4. Restart Unity (if already open)"
        echo "5. Press Play to test!"
        echo ""
    else
        echo ""
        echo "=================================================="
        echo "❌ ERROR: Could not find built library file"
        echo "=================================================="
        echo "Expected to find library matching pattern:"
        if [ "$PLATFORM" = "macOS" ]; then
            echo "  libReplicantDriveSim.dylib"
        elif [ "$PLATFORM" = "Linux" ]; then
            echo "  libReplicantDriveSim.so"
        else
            echo "  ReplicantDriveSim.dll"
        fi
        echo ""
        echo "Files in build directory:"
        ls -lh
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

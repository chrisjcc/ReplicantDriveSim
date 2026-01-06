#!/bin/bash

# Unity 6 Build Automation Script for ReplicantDriveSim
# This script automates the build process for Unity 6 applications
# Supports macOS, Linux, and Windows build targets

set -e  # Exit on error

echo "=================================================="
echo "ReplicantDriveSim - Unity 6 Build Script"
echo "=================================================="

# ============================================
# Configuration
# ============================================

# Unity 6 Editor Path (default locations)
if [ "$(uname)" == "Darwin" ]; then
    # macOS
    UNITY_PATH="${UNITY_PATH:-/Applications/Unity/Hub/Editor/6000.0.30f1/Unity.app/Contents/MacOS/Unity}"
    DEFAULT_BUILD_TARGET="StandaloneOSX"
elif [ "$(expr substr $(uname -s) 1 5)" == "Linux" ]; then
    # Linux
    UNITY_PATH="${UNITY_PATH:-/opt/Unity/Editor/6000.0.30f1/Editor/Unity}"
    DEFAULT_BUILD_TARGET="StandaloneLinux64"
else
    # Windows (Git Bash/MSYS)
    UNITY_PATH="${UNITY_PATH:-C:/Program Files/Unity/Hub/Editor/6000.0.30f1/Editor/Unity.exe}"
    DEFAULT_BUILD_TARGET="StandaloneWindows64"
fi

# Project configuration
PROJECT_PATH="${PROJECT_PATH:-$(pwd)}"
BUILD_TARGET="${BUILD_TARGET:-${DEFAULT_BUILD_TARGET}}"
BUILD_OUTPUT="${BUILD_OUTPUT:-${PROJECT_PATH}/replicantdrivesim/Builds}"
LOG_DIR="${LOG_DIR:-${PROJECT_PATH}/Logs}"
LOG_FILE="${LOG_FILE:-${LOG_DIR}/unity_build_$(date +%Y%m%d_%H%M%S).log}"
BUILD_METHOD="${BUILD_METHOD:-UnityBuilderAction.BuildScript.PerformBuild}"

# Build options
BATCH_MODE="${BATCH_MODE:-true}"
NO_GRAPHICS="${NO_GRAPHICS:-true}"
QUIT_AFTER="${QUIT_AFTER:-true}"

# ============================================
# Functions
# ============================================

print_usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Unity 6 Build Automation Script for ReplicantDriveSim

OPTIONS:
    -p, --project-path PATH      Path to Unity project (default: current directory)
    -t, --build-target TARGET    Build target platform (default: auto-detected)
                                 Options: StandaloneOSX, StandaloneLinux64,
                                         StandaloneWindows64, iOS, Android
    -o, --output PATH           Build output directory (default: ./Builds)
    -l, --log-file PATH         Log file path (default: ./Logs/unity_build_TIMESTAMP.log)
    -u, --unity-path PATH       Path to Unity editor executable
    -m, --build-method METHOD   Unity build method (default: UnityBuilderAction.BuildScript.PerformBuild)
    -h, --help                  Show this help message

ENVIRONMENT VARIABLES:
    UNITY_PATH       Path to Unity 6 editor executable
    PROJECT_PATH     Path to Unity project
    BUILD_TARGET     Target platform for build
    BUILD_OUTPUT     Output directory for builds
    LOG_DIR          Directory for log files

EXAMPLES:
    # Build for current platform (auto-detected)
    $0

    # Build for macOS with custom output
    $0 --build-target StandaloneOSX --output ./Builds/macOS

    # Build for Linux in CI environment
    UNITY_PATH=/opt/unity/6000.0.30f1/Unity $0 --build-target StandaloneLinux64

    # Build for Windows
    $0 --build-target StandaloneWindows64 --output ./Builds/Windows

EOF
}

check_unity_installation() {
    echo "Checking Unity installation..."

    if [ ! -f "$UNITY_PATH" ] && [ ! -x "$UNITY_PATH" ]; then
        echo ""
        echo "❌ ERROR: Unity 6 editor not found at: $UNITY_PATH"
        echo ""
        echo "Please install Unity 6 (6000.0.30f1) or set UNITY_PATH:"
        echo ""
        if [ "$(uname)" == "Darwin" ]; then
            echo "  macOS default: /Applications/Unity/Hub/Editor/6000.0.30f1/Unity.app/Contents/MacOS/Unity"
        elif [ "$(expr substr $(uname -s) 1 5)" == "Linux" ]; then
            echo "  Linux default: /opt/Unity/Editor/6000.0.30f1/Editor/Unity"
        else
            echo "  Windows default: C:/Program Files/Unity/Hub/Editor/6000.0.30f1/Editor/Unity.exe"
        fi
        echo ""
        echo "You can set a custom path:"
        echo "  export UNITY_PATH=/path/to/Unity"
        echo "  $0"
        exit 1
    fi

    echo "✓ Unity 6 found at: $UNITY_PATH"
}

check_project() {
    echo "Checking project..."

    if [ ! -d "$PROJECT_PATH" ]; then
        echo "❌ ERROR: Project directory not found: $PROJECT_PATH"
        exit 1
    fi

    if [ ! -d "$PROJECT_PATH/Assets" ]; then
        echo "❌ ERROR: Not a valid Unity project (Assets folder not found): $PROJECT_PATH"
        exit 1
    fi

    echo "✓ Project found at: $PROJECT_PATH"
}

create_directories() {
    echo "Creating build directories..."

    # Create output directory
    mkdir -p "$BUILD_OUTPUT"
    echo "✓ Build output: $BUILD_OUTPUT"

    # Create logs directory
    mkdir -p "$LOG_DIR"
    echo "✓ Log directory: $LOG_DIR"
}

get_build_method_for_target() {
    local target=$1

    case "$target" in
        StandaloneOSX)
            echo "UnityBuilderAction.BuildScript.PerformMacOSBuild"
            ;;
        StandaloneLinux64)
            echo "UnityBuilderAction.BuildScript.PerformLinuxBuild"
            ;;
        StandaloneWindows64)
            echo "UnityBuilderAction.BuildScript.PerformWindowsBuild"
            ;;
        iOS)
            echo "UnityBuilderAction.BuildScript.PerformIOSBuild"
            ;;
        Android)
            echo "UnityBuilderAction.BuildScript.PerformAndroidBuild"
            ;;
        *)
            echo "UnityBuilderAction.BuildScript.PerformBuild"
            ;;
    esac
}

build_unity_project() {
    echo ""
    echo "=================================================="
    echo "Starting Unity Build"
    echo "=================================================="
    echo "Target Platform: $BUILD_TARGET"
    echo "Output Directory: $BUILD_OUTPUT"
    echo "Log File: $LOG_FILE"
    echo "Build Method: $BUILD_METHOD"
    echo "=================================================="
    echo ""

    # Construct Unity command
    local UNITY_CMD=("$UNITY_PATH")

    # Add batch mode flags
    if [ "$QUIT_AFTER" == "true" ]; then
        UNITY_CMD+=("-quit")
    fi

    if [ "$BATCH_MODE" == "true" ]; then
        UNITY_CMD+=("-batchmode")
    fi

    if [ "$NO_GRAPHICS" == "true" ]; then
        UNITY_CMD+=("-nographics")
    fi

    # Add project and build configuration
    UNITY_CMD+=(
        "-projectPath" "$PROJECT_PATH"
        "-buildTarget" "$BUILD_TARGET"
        "-executeMethod" "$BUILD_METHOD"
        "-logFile" "$LOG_FILE"
    )

    # Add custom build output path if needed
    if [ -n "$BUILD_OUTPUT" ]; then
        UNITY_CMD+=("-customBuildPath" "$BUILD_OUTPUT")
    fi

    echo "Executing Unity build..."
    echo "Command: ${UNITY_CMD[@]}"
    echo ""

    # Execute Unity build
    "${UNITY_CMD[@]}"

    local BUILD_RESULT=$?

    echo ""

    if [ $BUILD_RESULT -eq 0 ]; then
        echo "=================================================="
        echo "✅ Build Successful!"
        echo "=================================================="
        echo ""
        echo "Build artifacts location: $BUILD_OUTPUT"
        echo "Log file: $LOG_FILE"
        echo ""

        # List build output
        if [ -d "$BUILD_OUTPUT" ]; then
            echo "Build contents at $BUILD_OUTPUT:"
            ls -lh "$BUILD_OUTPUT" | head -20
        fi

        # Check if build ended up in the default root directory instead of BUILD_OUTPUT
        # This happens because BuildScript.cs has a hardcoded path
        ROOT_BUILDS="$PROJECT_PATH/Builds"
        if [ "$BUILD_OUTPUT" != "$ROOT_BUILDS" ] && [ -d "$ROOT_BUILDS" ]; then
            echo ""
            echo "Notice: Unity generated build in default location $ROOT_BUILDS"
            echo "Moving build results to $BUILD_OUTPUT for package compatibility..."
            mkdir -p "$BUILD_OUTPUT"
            cp -R "$ROOT_BUILDS/"* "$BUILD_OUTPUT/"
            rm -rf "$ROOT_BUILDS"
            echo "✓ Build results moved to $BUILD_OUTPUT"
        fi

        # Post-build: Copy Maps folder to build output
        echo ""
        echo "Post-build: Copying Maps folder..."
        MAPS_SRC="$PROJECT_PATH/Assets/Maps"
        
        if [ -d "$MAPS_SRC" ]; then
            # macOS .app bundles
            find "$BUILD_OUTPUT" -name "*.app" -type d | while read app; do
                if [ -d "$app/Contents" ]; then
                    echo "Copying Maps to $app/Contents/"
                    cp -R "$MAPS_SRC" "$app/Contents/"
                fi
            done
            
            # Windows/Linux Data folders (usually named <AppName>_Data)
            find "$BUILD_OUTPUT" -name "*_Data" -type d | while read data_dir; do
                echo "Copying Maps to $data_dir/"
                cp -R "$MAPS_SRC" "$data_dir/"
            done
            
            echo "✓ Maps folder copied"
        else
            echo "WARNING: Assets/Maps folder not found at $MAPS_SRC"
        fi

        return 0
    else
        echo "=================================================="
        echo "❌ Build Failed!"
        echo "=================================================="
        echo ""
        echo "Exit code: $BUILD_RESULT"
        echo "Log file: $LOG_FILE"
        echo ""

        # Show last 50 lines of log if it exists
        if [ -f "$LOG_FILE" ]; then
            echo "Last 50 lines of build log:"
            echo "-----------------------------------"
            tail -50 "$LOG_FILE"
        fi

        return $BUILD_RESULT
    fi
}

# ============================================
# Parse Command Line Arguments
# ============================================

while [[ $# -gt 0 ]]; do
    case $1 in
        -p|--project-path)
            PROJECT_PATH="$2"
            shift 2
            ;;
        -t|--build-target)
            BUILD_TARGET="$2"
            shift 2
            ;;
        -o|--output)
            BUILD_OUTPUT="$2"
            shift 2
            ;;
        -l|--log-file)
            LOG_FILE="$2"
            LOG_DIR=$(dirname "$LOG_FILE")
            shift 2
            ;;
        -u|--unity-path)
            UNITY_PATH="$2"
            shift 2
            ;;
        -m|--build-method)
            BUILD_METHOD="$2"
            shift 2
            ;;
        -h|--help)
            print_usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            print_usage
            exit 1
            ;;
    esac
done

# ============================================
# Main Execution
# ============================================

# If BUILD_METHOD is default, determine based on target
if [ "$BUILD_METHOD" == "UnityBuilderAction.BuildScript.PerformBuild" ]; then
    BUILD_METHOD=$(get_build_method_for_target "$BUILD_TARGET")
fi

# Run build process
check_unity_installation
check_project
create_directories
build_unity_project

exit $?

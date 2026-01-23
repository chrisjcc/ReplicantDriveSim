#!/bin/bash

# Script to build native libraries and deploy them properly for Unity
# This prevents duplicate plugin errors

echo "Building native libraries..."

# Build from the TrafficSimulation directory
cd Assets/Plugins/TrafficSimulation/build
make -j8

if [ $? -eq 0 ]; then
    echo "Build successful. Deploying libraries..."

    # Copy libraries to Unity plugins directory (overwrite existing)
    cp libReplicantDriveSim.dylib ../
    cp ../libOpenDrive/libOpenDrive.dylib ../ 2>/dev/null || cp libOpenDrive.dylib ../

    # Remove build directory copies to prevent Unity duplicate plugin errors
    rm -f libReplicantDriveSim*.dylib*
    rm -f libOpenDrive*.dylib*

    # Sign the libraries
    cd ..
    codesign --sign - --force libReplicantDriveSim.dylib libOpenDrive.dylib

    echo "Libraries deployed and signed successfully!"
    echo "You can now run Unity Editor or build the app."
else
    echo "Build failed!"
    exit 1
fi
---
layout: default
title: "Traffic Architecture"
permalink: Plugins/TrafficSimulation/
---

# Build and Install the Traffic Simulation Library

This directory contains the `libReplicantDriveSim` plugin for Unity, integrating `libOpenDRIVE` for OpenDRIVE map support.

<img src="https://raw.githubusercontent.com/chrisjcc/ReplicantDriveSim/main/External/images/NISSAN-GTR_ReplicantDriveSim_Raycasting.png" alt="Nissan GTR" width="800" height="600"/>

## Table of Contents
- [Prerequisites](#prerequisites)
  - [Verifying Git LFS Installation](#verifying-git-lfs-installation)
  - [Initializing Submodules](#initialize-submodules)
- [Building the Traffic Simulation Library](#build-the-traffic-simulation-library)
  - [MacOS Deployment Target](#macos-deployment-target)
  - [Standalone Traffic Library](#standalone-traffic-library)
- [Installing the Traffic Simulation Package](#install-traffic-simulation-package)
- [Example Usage](#example-usage)
- [Running Unit Tests](#run-unit-tests)
- [Versioning with Git Tags](#steps-to-create-a-git-tag-and-push-it-to-a-remote-repository)

# Prerequisites
- CMake 3.13 or higher
- Unity (version X.X.X)
- macOS (for building .dylib files)
- Git (for submodule management)

## Verifying Git LFS Installation

Before proceeding with the setup, ensure that Git LFS is installed and correctly initialized by running:

```shell
git lfs version
```

To verify that specific files are tracked by Git LFS, use:

```shell
git lfs ls-files
```

To fetch the required LFS files, run:

```shell
git lfs pull
```

## Initialize Submodules
To initialize and clone the submodule repositories along with checking out the correct commit as specified in the parent repository, run:

```shell
git submodule update --init --recursive
```

## Verifying Submodule Status
Once initialized, confirm that the submodules have been populated correctly by checking their status:

```shell
git submodule status
```

## Building the Traffic Simulation Library

### Build PIP Wheel
To build the Python package on macOS, set the macOS deployment target to avoid compatibility issues:

```shell
export MACOSX_DEPLOYMENT_TARGET=14.0
python -m build -v
unzip -l dist/simulation-*.whl
```

### Standalone Traffic Library Build
For building a standalone version of the traffic simulation library, follow these steps:

```shell
mkdir build
cd build
cmake -DCMAKE_BUILD_TYPE=Release ..
cmake --build . --config Release
cd ..
```

## Installing the Traffic Simulation Library
You can install the Traffic Simulation package directly from PyPI using pip:

```bash
pip install ReplicantDriveSim
```

Alternatively, to install the package in development mode from a local build:

```bash
pip install --force-reinstall dist/replicantdrivesim-*.whl
```

### Example Usage
Below is an example of how to use the traffic simulation library after

```python
# Import the compiled C++ module
import replicantdrivesim

# Create a traffic environment with 2 vehicles and seed value 42
traffic_sim = replicantdrivesim.Traffic(2, 42)

# Retrieve the current states of agents in the traffic environment
states = traffic_sim.get_agent_positions()

# Display the states of agents in the traffic environment
for agent, state in states.items():
    print(f"{agent} state: {state}")

# Advance the environment by one step
traffic_sim.step([1, 0], [[0.1, 0.2, 0.3], [0.0, 0.0, 0.0]])

# Update the states of agents in the traffic environment
states = traffic_sim.get_agent_positions()

# Display the updated states of agents in the traffic environment
for agent, state in states.items():
    print(f"{agent} state: {state}")
```

Alternatively, you can run the simulation using the provided Python scripts:

```shell
python simulacrum.py
```

Or:

```shell
python trainer.py
```

## Run unit tests
To run the unit tests, execute the following commands:

```shell
./build/tests/traffic_simulation_test
./build/tests/perception_module_test
```

Google Test provides robust features for writing and organizing unit tests in C++. Customize your test structure (`TEST_F`, `TEST`, etc.) as per your project requirements.

## Steps to Create a Git Tag and Push It to a Remote Repository

### Create a New Tag Locally
To create an annotated tag, run the following command. Replace v1.0.0 with your desired version number:

```shell
git tag -a v1.0.0 -m "Release version 1.0.0"
```
- -a `v1.0.0`: This option creates an annotated tag with the name `v1.0.0`.
- -m `Release version 1.0.0`: This option adds a message to the tag, which is stored with it.

### Push the Tag to the Remote Repository
After creating the tag locally, push it to the remote repository:

```shell
git push origin v1.0.0
```
- `origin`: The name of the remote repository (typically `origin` by default).
- `v1.0.0`: The name of the tag you created.

### Create a Release
You can follow these additional steps to fetch and verify tags from the remote repository:

```shell
git fetch --tags  # Retrieve all tags from the remote repository
git tag -a v0.1.0 -m "Version 0.1.0"  # Create a new annotated tag for version 0.1.0
git push origin v0.1.0  # Push the newly created tag to the remote repository
git fetch --tags  # Update local tags from the remote repository
git tag -l  # List all tags to verify the new tag
```

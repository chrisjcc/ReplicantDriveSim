---
layout: default
title: "Traffic Simulation"
permalink: /External/
---

# Build and Install Traffic Simulation Library

## Verifying Git LFS Installation

To check if Git LFS is correctly installed and initialized, you can run:
```shell
git lfs version
```

Check if specific files are being tracked by Git LFS using:
```shell
git lfs ls-files
```

Fetching LFS Files:
```shell
git lfs pull
```

## Initialize Submodules
This will initialize and clone the submodule repository and check out the appropriate commit specified in the parent repository.
```shell
git submodule update --init --recursive
```

## Verify the submodule status
Ensure that the submodule is now populated correctly.
```shell
git submodule status
```

## Environment setup
```shell
conda env create -f environment.yml
conda activate drive
```

# Build and check setup
```shell
export MACOSX_DEPLOYMENT_TARGET=14.0
TRAFFIC_SIM_SOURCEDIR=$PWD python -m build -v
unzip -l dist/simulation-*.whl
```

or configure and build a stand alone traffic library
```shell
mkdir build
cd build
cmake ..
make
cd ..
```
This will build the Google Test library (libgtest.a and libgtest_main.a).

## Install Traffic Simulation
```python
pip install --force-reinstall dist/simulation-*.whl
```

### Example Usage

```python
## Import the compiled C++ module
import traffic_simulation

## Create a traffic environment
simulation = traffic_simulation.TrafficSimulation(2)

## Retrieve the current states of agents in the traffic environment
states = simulation.get_agent_positions()

## Display the states of agents in the traffic environment
for agent, state in states.items():
    print(f"{agent} state: {state}")

## Advance the environment by one step
simulation.step([1, 0], [[0.1, 0.2, 0.3], [0.0, 0.0, 0.0]])

## Update the states of agents in the traffic environment
states = simulation.get_agent_positions()

## Display the updated states of agents in the traffic environment
for agent, state in states.items():
    print(f"{agent} state: {state}")
```

### Docker build
```shell
# Build docker image
export DOCKER_BUILDKIT=1 
docker-compose up --build
# Run docker image
docker run -it --rm -v $(pwd)/data:/app/repo/data replicantdrivesim-app bash

# Alternative build method
DOCKER_BUILDKIT=1 docker build --ssh default -t replicantdrivesim-app .

# Close docker container
docker-compose down
docker-compose down --volumes
docker-compose down --volumes --remove-orphans

docker system prune -a
docker system prune -a --volumes

docker-compose ps -a
docker-compose logs

# Exec into the Running Container (app is name of service in docker-compose.yml)
docker-compose exec app bash
```

```python
python simulacrum.py

# Or

python trainer.py
```

## Monitor simulation session
```shell
mlflow ui --backend-store-uri file:mlruns
```

## Run unit tests
```shell
./build/tests/traffic_simulation_test
./build/tests/perception_module_test
```
Google Test provides robust features for writing and organizing unit tests in C++. Customize your test structure (TEST_F, TEST, etc.) as per your project requirements.

# ReplicantDriveSim

ReplicantDriveSim is an advanced traffic simulation project designed for autonomous driving research. It leverages reinforcement learning, imitation learning, and computer vision to create realistic traffic scenarios and synthetic driving data. The simulation environment is built using Pygame for visualization and Miniforge for Python package management, ensuring a seamless development and deployment experience. This Docker image provides a fully configured environment with all necessary dependencies, enabling efficient experimentation and development of autonomous driving algorithms.

# Initialize Submodules
Use the following command to initialize and clone any submodules defined in the repository.
```shell
git submodule update --init --recursive
 ```

# Environment setup
```shell
conda env create -f environment.yml
conda activate drive
```

# Build and check setup
```shell
TRAFFIC_SIM_SOURCEDIR=$PWD python -m build -v
unzip -l dist/traffic_simulation-*.whl
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

# Install Traffic Simulation
```python
pip install --force-reinstall dist/traffic_simulation-*.whl
```

## Example Usage

```python
# Import the compiled C++ module
import traffic_simulation

# Create a traffic environment
simulation = traffic_simulation.TrafficSimulation(2)

# Retrieve the current states of agents in the traffic environment
states = simulation.get_agent_positions()

# Display the states of agents in the traffic environment
for agent, state in states.items():
    print(f"{agent} state: {state}")

# Advance the environment by one step
simulation.step([1, 0], [[0.1, 0.2, 0.3], [0.0, 0.0, 0.0]])

# Update the states of agents in the traffic environment
states = simulation.get_agent_positions()

# Display the updated states of agents in the traffic environment
for agent, state in states.items():
    print(f"{agent} state: {state}")
```

## Docker build
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

# Monitor simulation session
```shell
mlflow ui --backend-store-uri file:mlruns
```

# Run unit tests
```shell
./build/tests/traffic_simulation_test
```
Google Test provides robust features for writing and organizing unit tests in C++. Customize your test structure (TEST_F, TEST, etc.) as per your project requirements.

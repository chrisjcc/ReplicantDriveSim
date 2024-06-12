# ReplicantDriveSim
Autonomous driving simulations development

# Environment setup
```shell
conda env create -f environment.yml
conda activate drive
```

# Build and check setup
```shell
python -m build -v
unzip -l dist/traffic_simulation-*.whl
```

or configure and build a stand alone traffic library
```shell
mkdir build
cd build
cmake ..
make
```

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

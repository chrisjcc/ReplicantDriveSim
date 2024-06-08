# ReplicantDriveSim
Autonomous driving simulations development


## Install cmake and pybind
```python
brew install cmake
pip install pybind11
```

```python
mkdir build
cd build
cmake ..
make
```

## Verify Build Output
Ensure that the shared library was created successfully. After running `cmake ..` and `make`, you should see a `.so` file (or `.dylib` on macOS) in the build directory.

For example, you might see a file named `traffic_simulation.cpython-39-darwin.so` or `traffic_simulation.so`.

## Set PYTHONPATH
Make sure the directory containing the compiled module is in your `PYTHONPATH`. You can temporarily add it to `PYTHONPATH` when running your Python script.

```python
export PYTHONPATH=$PYTHONPATH:/path/to/your/build/directory
```

Alternatively, you can modify your Python script to add the build directory to the system path at runtime:

```python
import sys
sys.path.append('/path/to/your/build/directory')

import traffic_simulation  # Import the compiled C++ module

# Example usage
simulation = traffic_simulation.TrafficSimulation(2)
simulation.step([1, 0], [[0.1, 0.2, 0.3], [0.0, 0.0, 0.0]])
states = simulation.getStates()
for state in states:
    print(state.x, state.y, state.vx, state.vy)

```

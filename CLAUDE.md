# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Essential Commands

### Development Environment Setup
```bash
# Create conda environment
conda env create -f environment.yml
conda activate drive

# Install package (development mode)
make install
# Alternative: make install-dev  (includes dev dependencies)
```

### Building Components
```bash
# Build C++ native library for Unity
make build-native

# Build Unity standalone application
make build-unity

# Build everything
make build-all

# Rebuild after cleaning
make rebuild-all
```

### Testing and Quality
```bash
# Run tests
make test
make test-verbose          # Verbose output
make test-cov             # With coverage

# Code quality checks
make lint                 # flake8 linting
make format               # black + isort formatting
make type-check           # mypy type checking
make check                # All quality checks
```

### Submodules
```bash
# Initialize and update git submodules (libOpenDRIVE)
make submodule
```

## Architecture Overview

### Multi-Language Integration
ReplicantDriveSim is a sophisticated traffic simulation system integrating three main components:
- **Unity 6 (C#)**: Frontend simulation environment and ML-Agents integration
- **C++**: High-performance traffic simulation backend with OpenDRIVE map support
- **Python**: RL training interface, distributed via PyPI

### Key Components

#### 1. Unity Frontend (`Assets/`)
- **TrafficManager.cs**: Central coordinator that steps the simulation and manages all agents
- **TrafficAgent.cs**: ML-Agents implementation for individual vehicle behaviors
- **OpenDriveRenderer.cs**: Renders OpenDRIVE maps in Unity
- **MapAccessorRenderer.cs**: Handles map rendering and visualization
- **ML-Agents Integration**: Uses Unity ML-Agents 4.0.0 for RL training

#### 2. Native C++ Backend (`Assets/Plugins/TrafficSimulation/`)
Built as shared libraries (.dylib/.so/.dll) consumed by Unity:
- **traffic.cpp**: Core traffic simulation logic
- **vehicle.cpp**: Vehicle behavior and dynamics
- **simulation.cpp**: Simulation state management
- **bicycle_model.cpp**: Vehicle physics model
- **OpenDriveWrapper.cpp**: OpenDRIVE map integration
- **MapAccessor.cpp**: Map data access interface
- **traffic_simulation_c_api.cpp**: C API for Unity interop
- **bindings.cpp**: Python bindings via pybind11

#### 3. libOpenDRIVE Integration (`Assets/Plugins/libOpenDRIVE/`)
Git submodule providing OpenDRIVE map format support:
- Parses .xodr files for realistic road networks
- Provides geometric and semantic road information
- Built as shared library linked to main simulation

#### 4. Python Package (`replicantdrivesim/`)
- Ray RLlib integration for distributed RL training
- Gymnasium environment wrapper
- PyPI-distributed package with Unity builds bundled
- CLI interface for training and evaluation

### Build System Architecture

#### CMake Configuration
Dual-target build system in `Assets/Plugins/TrafficSimulation/CMakeLists.txt`:
- **Unity Target**: Shared library for Unity integration
- **PyPI Target**: Python module with pybind11 bindings
- **libOpenDRIVE**: Automatically built as dependency

#### Makefile Automation
Comprehensive automation in root `Makefile`:
- Multi-platform support (macOS, Linux, Windows)
- Dependency management with special handling for Apple Silicon
- Automated testing and quality checks
- Distribution building for PyPI

### Dependencies and Version Constraints

#### Critical Version Requirements
- **Unity**: 6000.0.30f1 (Unity 6)
- **Python**: 3.10.1-3.10.12 (ML-Agents requirement)
- **ML-Agents**: 4.0.0 (Unity), 1.1.0 (Python)
- **Ray RLlib**: 2.31.0
- **CMake**: >=3.29

#### Platform-Specific Considerations
- **Apple Silicon**: Complex dependency workaround in Makefile for gym/mlagents compatibility
- **OpenDRIVE**: Uses @loader_path linking on macOS for proper dylib resolution
- **Unity Builds**: Platform-specific output directories (StandaloneOSX, StandaloneLinux64, etc.)

## Development Workflow

### Working with Native Code
1. Modify C++ source in `Assets/Plugins/TrafficSimulation/src/`
2. Build native library: `make build-native`
3. Unity will automatically detect updated library

### Working with Unity
1. Open project in Unity 6 (6000.0.30f1)
2. Main scene: `Assets/Scenes/SampleScene.unity`
3. Key GameObjects: TrafficManager, TrafficAgent prefabs
4. Build standalone: `make build-unity`

### Working with Python Package
1. Install in development mode: `make install`
2. Run tests: `make test`
3. Build distribution: `make dist`

### Git Submodules
- libOpenDRIVE is a git submodule
- Always run `make submodule` after fresh clone
- Submodule updates require both parent and submodule commits

## Debugging and Troubleshooting

### Common Issues
- **Build failures**: Check CMake, Unity versions match requirements
- **Apple Silicon**: Use special install sequence in Makefile
- **Missing symbols**: Ensure libOpenDRIVE submodule initialized
- **Unity crashes**: Check native library compatibility with Unity version
- **Import errors**: Verify Python version 3.10.1-3.10.12 exact range

### Build Artifacts
- Native libraries: `Assets/Plugins/TrafficSimulation/build/`
- Unity builds: `Builds/StandaloneOSX/`, etc.
- Python packages: `dist/`
- Logs: `build_native_*_log.txt`
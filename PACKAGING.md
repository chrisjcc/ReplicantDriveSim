# Python Packaging & Build System Guide

This document describes the modern Python packaging setup for ReplicantDriveSim, following PEP 517/518/621/660 standards.

---

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Package Configuration](#package-configuration)
- [Installation Methods](#installation-methods)
- [Build System](#build-system)
- [Development Workflow](#development-workflow)
- [Conda Environment](#conda-environment)
- [Makefile Targets](#makefile-targets)
- [PyPI Publishing](#pypi-publishing)
- [Migration Notes](#migration-notes)

---

## Overview

### Modern Packaging Standards

ReplicantDriveSim now uses modern Python packaging standards:

- **PEP 517**: Build system specification
- **PEP 518**: Declaring build dependencies
- **PEP 621**: Project metadata in `pyproject.toml`
- **PEP 660**: Editable installs for development

### Key Changes

✅ **Single source of truth**: All configuration in `pyproject.toml`
✅ **No redundancy**: `environment.yml` references `pyproject.toml` for Python dependencies
✅ **Deprecated files**: Removed `setup.cfg`, minimized `setup.py`
✅ **Build automation**: Comprehensive `Makefile` for common tasks
✅ **PyPI ready**: Production-grade packaging for distribution

---

## Quick Start

### Using Makefile (Recommended)

```bash
# Install in editable mode (development)
make install

# Install with all dependencies
make install-all

# Build native library and Unity app
make build-all

# Run tests
make test

# Get help
make help
```

### Using pip Directly

```bash
# Editable install (development)
pip install -e .

# Production install
pip install .

# With optional dependencies
pip install -e .[dev]      # Development tools
pip install -e .[docs]     # Documentation tools
pip install -e .[mlflow]   # MLflow tracking
pip install -e .[all]      # Everything
```

### Using Conda

```bash
# Create environment (includes package installation)
conda env create -f environment.yml

# Activate environment
conda activate drive

# Update environment
make conda-update
```

---

## Package Configuration

### pyproject.toml Structure

All package configuration is centralized in `pyproject.toml`:

```toml
[build-system]
requires = ["setuptools>=75.1.0", "wheel", "pybind11>=2.12.0"]
build-backend = "setuptools.build_meta"

[project]
name = "replicantdrivesim"
version = "0.6.6"
description = "A Unity-based traffic simulation environment..."
readme = "README.md"
requires-python = ">=3.10.1,<=3.10.12"
license = {text = "MIT"}

[project.dependencies]
# Core runtime dependencies

[project.optional-dependencies]
dev = [...]    # Development tools (pytest, black, mypy, etc.)
docs = [...]   # Documentation tools (sphinx, etc.)
mlflow = [...] # MLflow experiment tracking
all = ["replicantdrivesim[dev,docs,mlflow]"]

[project.scripts]
replicantdrivesim = "replicantdrivesim.cli:main"

[tool.setuptools.package-data]
replicantdrivesim = [
    "*.so", "*.dll", "*.dylib",
    "configs/*.yaml",
    "Builds/**/*",
]
```

### Core Dependencies

- **mlagents 1.1.0**: Unity ML-Agents Python SDK
- **gymnasium 0.26.3**: RL environment API
- **ray[rllib] 2.31.0**: Distributed RL framework
- **numpy 1.23.5**: Numerical computing
- **torch**: Deep learning framework
- **pybind11**: C++/Python bindings

### Python Version Requirement

**Python 3.10.1 - 3.10.12** (strict requirement for ML-Agents compatibility)

---

## Installation Methods

### Development Installation (Editable)

**Recommended for active development:**

```bash
# Minimal install
make install
# or
pip install -e .

# With development tools
make install-dev
# or
pip install -e .[dev]
```

**Benefits:**
- Changes to Python code immediately reflected
- No need to reinstall after editing
- Supports debugging with breakpoints

### Production Installation

**For deployment or end-users:**

```bash
make install-prod
# or
pip install .
```

**Benefits:**
- Faster imports (no symlinks)
- Isolated from source changes
- Suitable for containers/deployments

### From Source Distribution

```bash
# Build distributions
make dist

# Install from wheel
pip install dist/replicantdrivesim-0.6.6-py3-none-any.whl
```

---

## Build System

### Native Library (C++ Traffic Simulation)

The project includes a C++ native library for high-performance vehicle dynamics simulation.

**Build manually:**
```bash
./build_native_library.sh
```

**Build with Make:**
```bash
make build-native
```

**Build artifacts:**
- `Assets/Plugins/TrafficSimulation/build/` - CMake build directory
- `*.so` (Linux), `*.dylib` (macOS), `*.dll` (Windows) - Compiled libraries

**Clean and rebuild:**
```bash
make rebuild-native
```

### Unity Application

Unity standalone builds for the simulation environment.

**Build manually:**
```bash
./build_unity_app.sh
```

**Build with Make:**
```bash
make build-unity
```

**Build artifacts:**
- `Builds/StandaloneOSX/` - macOS build
- `Builds/StandaloneLinux64/` - Linux build
- `Builds/StandaloneWindows64/` - Windows build

**Clean and rebuild:**
```bash
make rebuild-unity
```

### Build Everything

```bash
# Build both native library and Unity app
make build-all

# Clean everything and rebuild
make rebuild-all
```

---

## Development Workflow

### Initial Setup

```bash
# Clone repository
git clone https://github.com/chrisjcc/ReplicantDriveSim.git
cd ReplicantDriveSim

# Create conda environment
conda env create -f environment.yml
conda activate drive

# Or use make
make conda-env
conda activate drive

# Build native library
make build-native

# Build Unity application (optional)
make build-unity
```

### Daily Development

```bash
# Activate environment
conda activate drive

# Make code changes...

# Run tests
make test

# Format code
make format

# Run linting
make lint

# Type checking
make type-check

# Run all checks
make check
```

### Before Committing

```bash
# Format and check code
make format
make check

# Run full test suite with coverage
make test-cov

# Ensure builds work
make build-all
```

---

## Conda Environment

### Design Philosophy

The `environment.yml` file follows a "single source of truth" approach:

- **Conda dependencies**: System libraries, Python interpreter, build tools
- **Pip dependencies**: Reference to `pyproject.toml` (avoids duplication)

### Structure

```yaml
name: drive
channels:
  - conda-forge
  - defaults

dependencies:
  # System libraries (conda-only)
  - python=3.10.12
  - cmake>=3.29.0
  - ...

  # Python packages from pyproject.toml
  - pip:
      - -e .[all]  # Install from pyproject.toml
```

### Why This Approach?

✅ **No duplication**: Python dependencies declared once in `pyproject.toml`
✅ **Consistency**: Conda and pip users get same versions
✅ **Maintainability**: Update dependencies in one place
✅ **Flexibility**: Can install with/without conda

### Usage

```bash
# Create environment
make conda-env
# or
conda env create -f environment.yml

# Update environment after changing pyproject.toml
make conda-update
# or
conda env update -f environment.yml --prune

# Remove environment
conda env remove -n drive
```

---

## Makefile Targets

### Installation

| Target | Description |
|--------|-------------|
| `make install` | Install in editable mode (development) |
| `make install-prod` | Production install (non-editable) |
| `make install-dev` | Install with dev dependencies |
| `make install-all` | Install with all optional dependencies |
| `make uninstall` | Uninstall package |

### Build

| Target | Description |
|--------|-------------|
| `make build-native` | Build C++ native library |
| `make build-unity` | Build Unity application |
| `make build-all` | Build both native library and Unity app |
| `make rebuild-native` | Clean and rebuild native library |
| `make rebuild-unity` | Clean and rebuild Unity app |
| `make rebuild-all` | Clean and rebuild everything |

### Testing

| Target | Description |
|--------|-------------|
| `make test` | Run pytest test suite |
| `make test-cov` | Run tests with coverage report |
| `make test-verbose` | Run tests in verbose mode |

### Code Quality

| Target | Description |
|--------|-------------|
| `make lint` | Run flake8 linting |
| `make format` | Format code with black and isort |
| `make format-check` | Check formatting without changes |
| `make type-check` | Run mypy type checking |
| `make check` | Run all quality checks |

### Documentation

| Target | Description |
|--------|-------------|
| `make docs` | Build Sphinx documentation |
| `make docs-serve` | Build and serve docs at localhost:8000 |

### Cleanup

| Target | Description |
|--------|-------------|
| `make clean` | Remove Python build artifacts |
| `make clean-native` | Remove native library artifacts |
| `make clean-unity` | Remove Unity build artifacts |
| `make clean-test` | Remove test/coverage artifacts |
| `make clean-docs` | Remove documentation artifacts |
| `make clean-all` | Remove all build artifacts |

### Development

| Target | Description |
|--------|-------------|
| `make dev-setup` | Complete development environment setup |
| `make conda-env` | Create conda environment |
| `make conda-update` | Update conda environment |

### CI/CD

| Target | Description |
|--------|-------------|
| `make ci-test` | Run tests in CI mode (XML coverage) |
| `make ci-build` | CI build target (everything) |

### Package Management

| Target | Description |
|--------|-------------|
| `make dist` | Build source and wheel distributions |
| `make upload-test` | Upload to TestPyPI |
| `make upload` | Upload to PyPI |

### Information

| Target | Description |
|--------|-------------|
| `make show-deps` | Show installed dependencies |
| `make show-version` | Show package version |
| `make show-info` | Show package information |

---

## PyPI Publishing

### Prerequisites

```bash
# Install build and upload tools
pip install build twine
```

### Build Distributions

```bash
# Build source distribution and wheel
make dist

# Check distributions
ls -lh dist/
# replicantdrivesim-0.6.6.tar.gz
# replicantdrivesim-0.6.6-py3-none-any.whl
```

### Test on TestPyPI

```bash
# Upload to TestPyPI
make upload-test

# Test installation from TestPyPI
pip install --index-url https://test.pypi.org/simple/ replicantdrivesim
```

### Publish to PyPI

```bash
# Upload to PyPI (production)
make upload

# Install from PyPI
pip install replicantdrivesim
```

### Version Bumping

Update version in `pyproject.toml`:

```toml
[project]
version = "0.6.7"  # Increment version
```

Then rebuild and publish:

```bash
make clean-all
make dist
make upload
```

---

## Migration Notes

### What Changed?

#### ✅ Added

- **`pyproject.toml`**: Complete PEP 621 compliant configuration (199 lines)
- **`Makefile`**: Comprehensive build automation (200+ lines)
- **`PACKAGING.md`**: This documentation file

#### 🔄 Modified

- **`environment.yml`**: Now references `pyproject.toml` for Python deps (47 lines, down from 116)
- **`setup.py`**: Minimized to stub with documentation (23 lines, down from 7 functional)

#### ❌ Removed

- **`setup.cfg`**: Deprecated per PEP 621 (all config moved to `pyproject.toml`)

### Backward Compatibility

✅ **pip install**: Still works exactly as before
✅ **pip install -e .**: Editable installs unchanged
✅ **python setup.py**: Still functional (legacy support)
✅ **Existing code**: No changes to package imports or usage

### For Contributors

**Before:**
```bash
pip install -e .
```

**After (same command works, but now preferred):**
```bash
make install
```

**Before:**
```bash
./build_native_library.sh
./build_unity_app.sh
```

**After (same scripts work, but now wrapped):**
```bash
make build-all
```

### For CI/CD

**Before:**
```yaml
- pip install -e .[dev]
- pytest
```

**After (equivalent):**
```yaml
- make install-dev
- make ci-test
```

---

## Troubleshooting

### Python Version Mismatch

**Error:**
```
ERROR: Package 'replicantdrivesim' requires a different Python: 3.11.x not in '<=3.10.12,>=3.10.1'
```

**Solution:**
Use Python 3.10.1-3.10.12 (required for ML-Agents):
```bash
conda env create -f environment.yml  # Installs Python 3.10.12
conda activate drive
```

### Missing Build Dependencies

**Error:**
```
ModuleNotFoundError: No module named 'pybind11'
```

**Solution:**
Install build dependencies:
```bash
pip install -r requirements-build.txt
# or
make install-dev
```

### Native Library Build Fails

**Error:**
```
CMake Error: Could not find CMAKE_ROOT
```

**Solution:**
Install CMake:
```bash
conda install cmake
# or
brew install cmake  # macOS
sudo apt install cmake  # Ubuntu
```

### Unity Build Fails

**Error:**
```
Unity not found in PATH
```

**Solution:**
Ensure Unity 6 (6000.0.30f1) is installed and in PATH:
```bash
export PATH="/Applications/Unity/Hub/Editor/6000.0.30f1/Unity.app/Contents/MacOS:$PATH"
```

---

## Best Practices

### Dependency Management

✅ **DO**: Declare dependencies in `pyproject.toml`
✅ **DO**: Use version constraints for stability (`mlagents==1.1.0`)
✅ **DO**: Pin exact versions for reproducibility
❌ **DON'T**: Duplicate dependencies in multiple files
❌ **DON'T**: Use `requirements.txt` for package dependencies

### Development Workflow

✅ **DO**: Use editable installs (`make install`)
✅ **DO**: Run tests before committing (`make test`)
✅ **DO**: Format code automatically (`make format`)
✅ **DO**: Use Makefile for common tasks
❌ **DON'T**: Edit installed package files
❌ **DON'T**: Skip linting/type checking

### Build Automation

✅ **DO**: Use Makefile targets for builds
✅ **DO**: Clean before rebuilding (`make rebuild-all`)
✅ **DO**: Verify builds in CI/CD
❌ **DON'T**: Commit build artifacts to git
❌ **DON'T**: Manually run build scripts in production

---

## Additional Resources

### Documentation

- **Official Docs**: https://readthedocs.org/projects/replicantdrivesim
- **GitHub**: https://github.com/chrisjcc/ReplicantDriveSim
- **Issues**: https://github.com/chrisjcc/ReplicantDriveSim/issues

### Python Packaging Standards

- [PEP 517: Build System](https://peps.python.org/pep-0517/)
- [PEP 518: Build Dependencies](https://peps.python.org/pep-0518/)
- [PEP 621: Project Metadata](https://peps.python.org/pep-0621/)
- [PEP 660: Editable Installs](https://peps.python.org/pep-0660/)

### Tools Documentation

- [setuptools](https://setuptools.pypa.io/)
- [pip](https://pip.pypa.io/)
- [conda](https://docs.conda.io/)
- [make](https://www.gnu.org/software/make/)

---

**Document Version**: 1.0
**Date**: 2026-01-01
**Author**: Claude (AI Assistant)
**Reviewed By**: Pending

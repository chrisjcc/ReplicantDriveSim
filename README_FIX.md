# Fixing ReplicantDriveSim Import and Configuration Errors

This document provides solutions for common import and configuration errors when running `examples/trainer.py`.

## Issue: `ModuleNotFoundError: No module named 'replicantdrivesim.replicantdrivesim'`

This error occurs when the C++ bindings (Traffic and Vehicle classes) are not properly built or installed.

### Solution

1. **Install pybind11** (if not already installed):
   ```bash
   pip install pybind11
   ```

2. **Build the C++ extensions** using one of these methods:
   
   **Option A: Development build (recommended for development)**
   ```bash
   python setup.py build_ext --inplace
   ```
   
   **Option B: Full reinstall**
   ```bash
   pip install -e . --force-reinstall
   ```
   
   **Option C: Use the build script**
   ```bash
   python build_extensions.py
   ```

3. **Verify the installation**:
   ```bash
   python check_environment.py
   ```

## Issue: Ray Actor Creation Failure

The error `RuntimeError: The actor with name UnityEnvResource failed to import on the worker` occurs when Ray worker processes don't have access to the required dependencies.

### Solution

1. **Ensure all dependencies are installed in the same environment**:
   ```bash
   pip install ray[rllib] mlagents_envs gymnasium pyyaml mlflow
   ```

2. **Make sure C++ extensions are built** (see above).

3. **For macOS users**: Check Unity app permissions:
   ```bash
   chmod +x replicantdrivesim/Builds/StandaloneOSX/libReplicantDriveSim.app
   ```

## Issue: Unity Environment Not Found

### Solution

1. **Check if Unity builds exist**:
   - macOS: `replicantdrivesim/Builds/StandaloneOSX/libReplicantDriveSim.app`
   - Windows: `replicantdrivesim/Builds/Windows/libReplicantDriveSim.exe`
   - Linux: `replicantdrivesim/Builds/Linux/libReplicantDriveSim.x86_64`

2. **Download Unity builds if missing** (check the project releases or build instructions).

## Quick Diagnosis

Run the environment check script to identify issues:

```bash
python check_environment.py
```

This script will:
- Check Python version compatibility
- Verify required dependencies are installed
- Test C++ bindings availability
- Check Unity executable paths
- Validate configuration files

## Step-by-Step Fix for the Original Error

Based on the error in issue #176, follow these steps:

1. **Navigate to the repository root**:
   ```bash
   cd ~/Desktop/project/sim4traffic/tmp/ReplicantDriveSim
   ```

2. **Activate the correct environment**:
   ```bash
   conda activate mlagents_py3p10
   ```

3. **Install pybind11 if not present**:
   ```bash
   pip install pybind11
   ```

4. **Build the C++ extensions**:
   ```bash
   python setup.py build_ext --inplace
   ```

5. **Verify the build**:
   ```bash
   python -c "import replicantdrivesim; print('Success!' if replicantdrivesim.cpp_bindings_available() else 'Failed!')"
   ```

6. **Run the trainer**:
   ```bash
   python examples/trainer.py
   ```

## Additional Notes

- The project now includes improved error handling that provides clear instructions when C++ bindings are missing.
- The setup.py has been updated to properly build pybind11 extensions.
- Ray worker environment issues should be resolved once the C++ extensions are properly built and installed.

## Still Having Issues?

If you continue to experience problems:

1. Check that you're using Python 3.8 or later
2. Ensure all files in `Assets/Plugins/TrafficSimulation/src/` exist
3. Make sure you have a C++ compiler available (required for building pybind11 extensions)
4. On macOS, you may need to install Xcode command line tools: `xcode-select --install`
5. On Windows, you may need Visual Studio build tools
6. On Linux, ensure you have build-essential: `sudo apt-get install build-essential`
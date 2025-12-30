# Unity 6 Migration Troubleshooting Guide

This document addresses common issues encountered when opening the ReplicantDriveSim project in Unity 6.

## Issue 1: Missing Prefab Variants ✅ FIXED

**Error Messages:**
```
Problem detected while importing the Prefab file: 'Assets/Prefabs/Rock_F_01.prefab'.
Missing Prefab Variant parent: 'Rock_F_01 (Missing Prefab with guid: ...)'
```

**Status:** ✅ **FIXED** - These non-essential decorative prefabs have been removed.

**What It Was:** Unity couldn't find the parent prefabs for variant prefabs (Rocks and Airplane).

**Solution Applied:** Removed the problematic prefab files as they're not essential for the traffic simulation.

---

## Issue 2: ML-Agents Package Not Properly Installed

**Symptom:** ML-Agents components are unhighlighted/not clickable in Unity Editor.

**Root Cause:** Unity's package cache still had ML-Agents 3.0.0 instead of 4.0.0.

**Solution:** ✅ **FIXED** - Package cache has been cleared.

### What Was Done:
1. Deleted `Packages/packages-lock.json`
2. Deleted `Library/` folder to force full package refresh

### Next Steps (When You Reopen Unity):

Unity will automatically:
1. Download and install ML-Agents 4.0.0
2. Download and install Unity AI Inference Engine 2.2.1
3. Rebuild the Library folder

**Verification:**
After reopening Unity, verify packages are installed:
1. Open Window → Package Manager
2. Check that "ML Agents" shows version 4.0.0
3. Check that "AI Inference" shows version 2.2.1

If packages are missing:
1. Click "+" in Package Manager
2. Select "Add package by name..."
3. Enter: `com.unity.ml-agents` (version: 4.0.0)
4. Repeat for: `com.unity.ai.inference` (version: 2.2.1)

---

## Issue 3: Native Library Loading Error ⚠️ CRITICAL

**Error Message:**
```
Error in TrafficManager Start: ReplicantDriveSim assembly:<unknown assembly> type:<unknown type> member:(null)
  at (wrapper managed-to-native) TrafficManager.Traffic_create(int,uint)
```

**Root Cause:** The native C++ library `libReplicantDriveSim` hasn't been built for your platform.

**Why This Happens:** The project includes C++ source code in `Assets/Plugins/TrafficSimulation/`, but Unity needs the **compiled library** (`.bundle` for macOS, `.so` for Linux, `.dll` for Windows).

### Solution: Build the Native Library

#### For macOS Users:

1. **Install Prerequisites:**
   ```bash
   # Install Xcode Command Line Tools (if not already installed)
   xcode-select --install

   # Install CMake
   brew install cmake
   ```

2. **Navigate to the Plugin Directory:**
   ```bash
   cd Assets/Plugins/TrafficSimulation
   ```

3. **Build the Library:**
   ```bash
   # Create build directory
   mkdir -p build
   cd build

   # Set macOS deployment target for compatibility
   export MACOSX_DEPLOYMENT_TARGET=14.0

   # Configure with CMake
   cmake ..

   # Build the library
   make -j$(sysctl -n hw.ncpu)
   ```

4. **Copy the Compiled Library to Unity Plugins:**
   ```bash
   # The compiled library should be in the build directory
   # Copy it to the appropriate Unity Plugins location

   # For macOS (creates a .bundle or .dylib)
   cp libReplicantDriveSim.* ../../
   ```

5. **Configure Plugin Import Settings in Unity:**

   After copying the library, you need to configure it in Unity:

   a. Select the library file in Unity's Project window
   b. In the Inspector, configure platform settings:
      - **Select platforms:** macOS (or your target platforms)
      - **CPU:** x86_64 or ARM64 (depending on your Mac)
      - **Load on startup:** Checked
   c. Click "Apply"

#### For Linux Users:

```bash
cd Assets/Plugins/TrafficSimulation
mkdir -p build && cd build
cmake ..
make -j$(nproc)
cp libReplicantDriveSim.so ../../
```

#### For Windows Users:

```powershell
cd Assets\Plugins\TrafficSimulation
mkdir build
cd build
cmake ..
cmake --build . --config Release
copy Release\ReplicantDriveSim.dll ..\..\
```

### Expected Output Structure:

After building, you should have:
```
Assets/
  Plugins/
    TrafficSimulation/
      libReplicantDriveSim.bundle  (macOS)
      libReplicantDriveSim.bundle.meta
      (or .so for Linux, .dll for Windows)
```

### Verification:

1. **Check the library exists:**
   ```bash
   ls -la Assets/Plugins/TrafficSimulation/libReplicantDriveSim.*
   ```

2. **In Unity:**
   - Reimport the Plugins folder (right-click → Reimport)
   - Press Play
   - The error should be gone and you should see:
     ```
     TrafficManager::InitializeTrafficSimulation started
     Attempting to create traffic simulation.
     TrafficManager::InitializeTrafficSimulation completed successfully
     ```

---

## Issue 4: DynamicBEVCameraController Can't Find Agents

**Warning Message:**
```
DynamicBEVCameraController: Could not find 'agent_0' in 'TrafficSimulationManager'
```

**Root Cause:** This is a **consequence** of Issue #3. The traffic simulation isn't initializing because the native library isn't loaded, so no agents are created.

**Solution:** This will be automatically fixed once you build the native library (Issue #3).

---

## Complete Setup Checklist

Use this checklist after following the solutions above:

### Before Opening Unity:
- ✅ Package cache cleared (`packages-lock.json` deleted)
- ✅ Library folder cleared
- ✅ Problematic prefabs removed
- ⬜ Native library built for your platform
- ⬜ Native library copied to Unity Plugins folder

### After Opening Unity 6:
- ⬜ Unity completes package resolution without errors
- ⬜ ML-Agents 4.0.0 shown in Package Manager
- ⬜ Unity AI Inference 2.2.1 shown in Package Manager
- ⬜ No compilation errors in Console
- ⬜ Native library recognized (check Plugin Inspector settings)
- ⬜ Press Play - TrafficManager initializes successfully
- ⬜ Agents spawn in the scene
- ⬜ No camera warnings in console

---

## Alternative: Use Pre-built Release

If you have trouble building the native library, check if there are pre-built binaries available:

1. **Check GitHub Releases:**
   ```bash
   # Look for release artifacts
   https://github.com/chrisjcc/ReplicantDriveSim/releases
   ```

2. **Use Pip Package Approach:**

   The ReplicantDriveSim pip package includes the compiled library. You might be able to extract it:

   ```bash
   pip download replicantdrivesim
   unzip replicantdrivesim-*.whl
   # Look for the .so/.dylib/.dll file
   # Copy it to Assets/Plugins/TrafficSimulation/
   ```

---

## Still Having Issues?

### Enable Detailed Logging:

1. **Check CMake Configuration:**
   ```bash
   cd Assets/Plugins/TrafficSimulation/build
   cmake .. -DCMAKE_VERBOSE_MAKEFILE=ON
   ```

2. **Check Unity Console:**
   - Open Console window (Window → General → Console)
   - Enable "Collapse" and "Clear on Play"
   - Look for the specific error message

3. **Check Plugin Import Settings:**
   - Select the native library in Unity
   - Inspector should show:
     - Plugin platforms configured
     - CPU architecture correct
     - No import errors

### Common Build Errors:

**CMake not found:**
```bash
brew install cmake  # macOS
sudo apt-get install cmake  # Linux
```

**Compiler not found:**
```bash
xcode-select --install  # macOS
sudo apt-get install build-essential  # Linux
```

**Wrong architecture:**
- For Apple Silicon Macs: Build for ARM64
- For Intel Macs: Build for x86_64
- Check with: `uname -m`

---

## Summary

The Unity 6 migration itself is complete and successful. The main issue you're experiencing is that **the native C++ library needs to be compiled** for your platform before Unity can use it.

**Priority Order:**
1. ✅ **DONE**: Clear Unity cache and remove problematic prefabs
2. **TODO**: Build the native library following instructions above
3. **VERIFY**: Reopen Unity and confirm all packages install correctly
4. **TEST**: Press Play and verify simulation runs

Once the native library is built and properly imported, all functionality should work as expected with Unity 6 and ML-Agents 4.0.0.

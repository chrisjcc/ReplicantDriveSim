# Unity 6 Build Automation for ReplicantDriveSim

This document describes the automated build system for creating Unity 6 standalone applications of the ReplicantDriveSim traffic simulation.

## Overview

The build automation system consists of:
- **`build_unity_app.sh`**: Shell script for automated building (macOS, Linux, Windows)
- **`Assets/Editor/BuildScript.cs`**: Unity C# build script with platform-specific configurations
- Support for both local development and CI/CD workflows (GitHub Actions)

## Quick Start

### Local Build (Auto-detect Platform)

```bash
./build_unity_app.sh
```

This will automatically:
- Detect your platform (macOS, Linux, or Windows)
- Find Unity 6 (6000.0.30f1) installation
- Build the application for your platform
- Save output to `./Builds/` directory
- Create build log in `./Logs/` directory

## Requirements

### Unity 6 Installation

**Unity Version**: 6000.0.30f1 or later

Default installation paths:
- **macOS**: `/Applications/Unity/Hub/Editor/6000.0.30f1/Unity.app/Contents/MacOS/Unity`
- **Linux**: `/opt/Unity/Editor/6000.0.30f1/Editor/Unity`
- **Windows**: `C:/Program Files/Unity/Hub/Editor/6000.0.30f1/Editor/Unity.exe`

If Unity is installed in a different location, set the `UNITY_PATH` environment variable:

```bash
export UNITY_PATH=/path/to/Unity
./build_unity_app.sh
```

### Prerequisites

- Unity 6 (6000.0.30f1) installed
- Xcode Command Line Tools (macOS)
- Build tools for your target platform
- Sufficient disk space (~2-5 GB for builds)

## Usage

### Command Line Options

```bash
./build_unity_app.sh [OPTIONS]
```

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `-p, --project-path PATH` | Path to Unity project | Current directory |
| `-t, --build-target TARGET` | Build target platform | Auto-detected |
| `-o, --output PATH` | Build output directory | `./Builds` |
| `-l, --log-file PATH` | Log file path | `./Logs/unity_build_TIMESTAMP.log` |
| `-u, --unity-path PATH` | Path to Unity editor | Auto-detected |
| `-m, --build-method METHOD` | Unity build method | Auto-selected |
| `-h, --help` | Show help message | - |

**Build Targets:**
- `StandaloneOSX` - macOS application (.app)
- `StandaloneLinux64` - Linux executable
- `StandaloneWindows64` - Windows executable (.exe)
- `iOS` - iOS application (macOS only)
- `Android` - Android APK

### Examples

#### Build for macOS

```bash
./build_unity_app.sh --build-target StandaloneOSX
```

Output: `./Builds/StandaloneOSX/libReplicantDriveSim.app`

#### Build for Linux

```bash
./build_unity_app.sh --build-target StandaloneLinux64
```

Output: `./Builds/StandaloneLinux64/libReplicantDriveSim`

#### Build for Windows

```bash
./build_unity_app.sh --build-target StandaloneWindows64
```

Output: `./Builds/StandaloneWindows64/libReplicantDriveSim.exe`

#### Build with Custom Output Directory

```bash
./build_unity_app.sh \
  --build-target StandaloneOSX \
  --output ~/Desktop/ReplicantDriveSim_Builds
```

#### Build in CI Environment

```bash
export UNITY_PATH=/opt/unity/6000.0.30f1/Unity
export PROJECT_PATH=/workspace/ReplicantDriveSim
export BUILD_OUTPUT=/workspace/builds

./build_unity_app.sh --build-target StandaloneLinux64
```

## Build Configuration

The build system uses the following configurations:

### Development Build (Default)

- **Resolution**: 2560x1440
- **Fullscreen Mode**: Windowed
- **Development Build**: Enabled
- **Allow Debugging**: Enabled
- **Architecture** (macOS): ARM64

### Release Build

To create a release build, modify `Assets/Editor/BuildScript.cs` or use the Unity Editor menu:
- **Build → Perform macOS Build (Release)**
- **Build → Perform Windows Build (Release)**
- **Build → Perform Linux Build (Release)**

Release builds have:
- Development Build: Disabled
- Allow Debugging: Disabled
- Optimized for performance

## CI/CD Integration

### GitHub Actions Example

Create `.github/workflows/build.yml`:

```yaml
name: Build Unity Application

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build-macos:
    runs-on: macos-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v3

      - name: Install Unity
        uses: game-ci/unity-builder@v4
        with:
          unityVersion: 6000.0.30f1

      - name: Build macOS Application
        run: |
          chmod +x build_unity_app.sh
          ./build_unity_app.sh --build-target StandaloneOSX

      - name: Upload Build Artifact
        uses: actions/upload-artifact@v3
        with:
          name: ReplicantDriveSim-macOS
          path: Builds/StandaloneOSX/

  build-linux:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v3

      - name: Install Unity
        uses: game-ci/unity-builder@v4
        with:
          unityVersion: 6000.0.30f1

      - name: Build Linux Application
        run: |
          chmod +x build_unity_app.sh
          ./build_unity_app.sh --build-target StandaloneLinux64

      - name: Upload Build Artifact
        uses: actions/upload-artifact@v3
        with:
          name: ReplicantDriveSim-Linux
          path: Builds/StandaloneLinux64/

  build-windows:
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v3

      - name: Install Unity
        uses: game-ci/unity-builder@v4
        with:
          unityVersion: 6000.0.30f1

      - name: Build Windows Application
        run: |
          bash build_unity_app.sh --build-target StandaloneWindows64

      - name: Upload Build Artifact
        uses: actions/upload-artifact@v3
        with:
          name: ReplicantDriveSim-Windows
          path: Builds/StandaloneWindows64/
```

## Environment Variables

The build script supports the following environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `UNITY_PATH` | Path to Unity editor executable | Platform-specific |
| `PROJECT_PATH` | Path to Unity project | Current directory |
| `BUILD_TARGET` | Target platform for build | Auto-detected |
| `BUILD_OUTPUT` | Output directory for builds | `./Builds` |
| `LOG_DIR` | Directory for log files | `./Logs` |
| `BUILD_METHOD` | Unity build method to call | Auto-selected |
| `BATCH_MODE` | Run Unity in batch mode | `true` |
| `NO_GRAPHICS` | Disable graphics in Unity | `true` |
| `QUIT_AFTER` | Quit Unity after build | `true` |

### Example with Environment Variables

```bash
export UNITY_PATH=/Applications/Unity/Hub/Editor/6000.0.30f1/Unity.app/Contents/MacOS/Unity
export BUILD_TARGET=StandaloneOSX
export BUILD_OUTPUT=~/Desktop/Builds

./build_unity_app.sh
```

## Build Output

### Directory Structure

```
Builds/
├── StandaloneOSX/
│   └── libReplicantDriveSim.app/
├── StandaloneLinux64/
│   └── libReplicantDriveSim
└── StandaloneWindows64/
    └── libReplicantDriveSim.exe

Logs/
└── unity_build_20241231_143000.log
```

### Build Artifacts

**macOS (.app)**:
- `libReplicantDriveSim.app` - Complete macOS application bundle
- Includes all assets, plugins, and dependencies
- Double-click to run

**Linux (executable)**:
- `libReplicantDriveSim` - Linux executable
- `libReplicantDriveSim_Data/` - Data directory
- Run with: `./libReplicantDriveSim`

**Windows (.exe)**:
- `libReplicantDriveSim.exe` - Windows executable
- `libReplicantDriveSim_Data/` - Data directory
- Double-click to run

## Troubleshooting

### Unity Not Found

**Error**: `❌ ERROR: Unity 6 editor not found`

**Solution**:
1. Install Unity 6 (6000.0.30f1) via Unity Hub
2. Or set `UNITY_PATH`:
   ```bash
   export UNITY_PATH=/path/to/Unity
   ```

### Build Failed

**Error**: `❌ Build Failed!`

**Solution**:
1. Check the log file location (shown in error output)
2. Review last 50 lines of the log
3. Common issues:
   - Missing scenes in build settings
   - Compiler errors in scripts
   - Missing assets or dependencies
   - Insufficient disk space

### Invalid Project Directory

**Error**: `❌ ERROR: Not a valid Unity project`

**Solution**:
- Ensure you're running from the project root
- Or use `-p` flag:
  ```bash
  ./build_unity_app.sh -p /path/to/project
  ```

### Permission Denied

**Error**: `Permission denied: build_unity_app.sh`

**Solution**:
```bash
chmod +x build_unity_app.sh
```

## Build Script Customization

### Modifying Build Configuration

Edit `Assets/Editor/BuildScript.cs` to customize:

```csharp
var config = new BuildConfiguration
{
    screenWidth = 3840,           // 4K resolution
    screenHeight = 2160,
    isDevelopmentBuild = false,   // Release build
    allowDebugging = false,
    isResizableWindow = true,
    fullScreenMode = FullScreenMode.ExclusiveFullScreen,
    architecture = Architecture.Universal  // macOS Universal (Intel + ARM)
};
```

### Adding Custom Build Methods

Add new build methods in `BuildScript.cs`:

```csharp
[MenuItem("Build/Perform iOS Build")]
public static void PerformIOSBuild()
{
    var config = new BuildConfiguration
    {
        isDevelopmentBuild = false,
        allowDebugging = false
    };
    PerformBuild(BuildTarget.iOS, BuildOptions.None, config);
}
```

### Custom Build Options

Modify the shell script to add custom Unity command-line arguments:

```bash
# In build_unity_app.sh, after line ~280
UNITY_CMD+=(
    "-customDefine" "MY_CUSTOM_DEFINE"
    "-enableCodeCoverage"
    "-burst-disable-compilation"
)
```

## Performance Tips

### Faster Builds

1. **Incremental Builds**: Don't clean the project between builds
2. **Reduce Log Verbosity**: Set `LOG_LEVEL=warn` in script
3. **Parallel Builds**: Build multiple platforms simultaneously (separate machines/containers)
4. **Cache Unity Library**: In CI, cache the `Library/` folder
5. **Use Release Builds**: Only for final distribution (dev builds are faster)

### Build Size Optimization

1. **Disable Development Build**: Reduces size by ~30%
2. **Strip Debug Symbols**: Configured automatically in release builds
3. **Compress Assets**: Unity's default compression is enabled
4. **Remove Unused Assets**: Clean up unused files before building

## Integration with Native Library

The Unity build includes the native C++ library (`libReplicantDriveSim`). Ensure it's built before creating Unity builds:

```bash
# Build native library first
./build_native_library.sh

# Then build Unity application
./build_unity_app.sh --build-target StandaloneOSX
```

The Unity build will automatically include the compiled native library from `Assets/Plugins/TrafficSimulation/`.

## Version Compatibility

| Component | Version | Notes |
|-----------|---------|-------|
| Unity | 6000.0.30f1+ | Unity 6 required |
| ML-Agents | 4.0.0 | Unity package |
| Unity AI Inference | 2.2.1 | For ML-Agents |
| macOS | 12.0+ | Deployment target 14.0 |
| Linux | Ubuntu 20.04+ | Any modern distro |
| Windows | 10/11 | 64-bit only |

## Support

For build-related issues:
1. Check the build log in `Logs/` directory
2. Review the troubleshooting section above
3. Consult Unity 6 documentation
4. Open an issue on the GitHub repository

## License

This build automation is part of ReplicantDriveSim, licensed under the MIT License.

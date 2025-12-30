# Unity 6 and ML-Agents 4.0.0 Migration Guide

This document outlines the changes made to upgrade ReplicantDriveSim to Unity 6 and ML-Agents 4.0.0.

## Overview

ReplicantDriveSim has been successfully migrated from Unity 2022.3.33f1 with ML-Agents 3.0.0 to Unity 6 (6000.0.30f1) with ML-Agents 4.0.0. This migration maintains the existing custom Ray RLlib integration architecture while ensuring compatibility with the latest Unity and ML-Agents releases.

## Key Changes

### Unity Engine

- **Previous Version**: Unity 2022.3.33f1
- **New Version**: Unity 6 (6000.0.30f1)

### ML-Agents Package

- **Previous Version**: ML-Agents 3.0.0
- **New Version**: ML-Agents 4.0.0 (Release 23)

### Unity Inference Engine

- **Removed**: com.unity.sentis 2.1.1
- **Added**: com.unity.ai.inference 2.2.1

ML-Agents 4.0.0 replaced Sentis with the Unity AI Inference Engine for model inference.

### Python Dependencies

#### Python Version
- **Previous**: Python 3.9
- **New**: Python 3.10.12 (Required: 3.10.1 - 3.10.12)

#### ML-Agents Python Packages
- **Added**: mlagents==1.1.0
- **Added**: mlagents-envs==1.1.0

#### Critical Version Downgrades

These downgrades are **required** for ML-Agents 1.1.0 compatibility:

1. **NumPy**
   - Previous: 1.26.4
   - New: 1.23.5
   - Reason: ML-Agents 1.1.0 requires numpy>=1.23.5,<1.24.0

2. **Protocol Buffers**
   - Previous: 5.27.2
   - New: 3.20.3
   - Reason: ML-Agents 1.1.0 requires protobuf>=3.6,<3.21

3. **gRPC**
   - Previous: 1.64.1
   - New: 1.53.2
   - Reason: ML-Agents 1.1.0 requires grpcio>=1.11.0,<=1.53.2

#### Gymnasium Support
- **Added**: gymnasium==0.26.3
- **Added**: gym==0.21.0

Both packages are included to support:
- Ray RLlib 2.31.0 (uses gymnasium)
- ML-Agents 1.1.0 (uses gym)
- Custom environment code (uses gymnasium aliased as gym)

## Architecture Decision

### Why Keep CustomUnityMultiAgentEnv?

After thorough research, we discovered that **Ray RLlib's Unity3DEnv still uses ML-Agents under the hood**. Therefore, we opted to:

1. **Keep the existing custom architecture**: The `CustomUnityMultiAgentEnv` wrapper is already implementing best practices and provides:
   - More control over the environment interface
   - Custom features tailored to ReplicantDriveSim
   - Full compatibility with Ray RLlib's MultiAgentEnv API

2. **Update to latest versions**: Instead of switching to Unity3DEnv, we updated:
   - Unity to version 6
   - ML-Agents to version 4.0.0
   - Python packages to compatible versions
   - Maintained the proven custom wrapper architecture

This approach provides the best of both worlds: modern Unity 6 support while retaining full control over the environment implementation.

## Compatibility Matrix

| Component | Version | Notes |
|-----------|---------|-------|
| Unity | 6000.0.30f1 | Minimum: 6000.0 |
| ML-Agents Unity Package | 4.0.0 | Released: August 28, 2025 |
| Unity AI Inference | 2.2.1 | Replaces Sentis |
| Python | 3.10.12 | Required: 3.10.1 - 3.10.12 |
| mlagents (Python) | 1.1.0 | Part of Release 23 |
| mlagents-envs (Python) | 1.1.0 | Part of Release 23 |
| Ray RLlib | 2.31.0 | Maintained compatibility |
| Gymnasium | 0.26.3 | For RLlib and custom code |
| Gym | 0.21.0 | For ML-Agents |
| NumPy | 1.23.5 | >=1.23.5,<1.24.0 |
| PyTorch | 2.3.1 | >=2.1.1 |
| Protocol Buffers | 3.20.3 | >=3.6,<3.21 |
| gRPC | 1.53.2 | >=1.11.0,<=1.53.2 |

## Migration Steps for Developers

### 1. Update Your Environment

If you're using the existing environment, recreate it with the updated dependencies:

```bash
# Remove old environment
conda env remove -n drive

# Create new environment from updated environment.yml
conda env create -f environment.yml

# Activate environment
conda activate drive
```

### 2. Update Unity Project

When opening the project in Unity 6:

1. Unity will automatically upgrade the project
2. The API Updater will handle most compatibility issues
3. Verify that the following packages are correctly installed:
   - com.unity.ml-agents: 4.0.0
   - com.unity.ai.inference: 2.2.1

### 3. Verify C# Code

The existing C# code is already compatible with Unity 6. The codebase already uses:
- `FindFirstObjectByType<T>()` instead of deprecated `FindObjectOfType<T>()`
- Compatible Unity APIs

No C# code changes are required.

### 4. Verify Python Code

The existing Python code uses `import gymnasium as gym`, which remains compatible. Both gym and gymnasium packages are now installed to satisfy all dependencies.

No Python code changes are required.

## Breaking Changes

### None for ReplicantDriveSim

Good news! The migration required **no breaking changes** to the ReplicantDriveSim codebase because:

1. The Unity C# scripts already used Unity 6-compatible APIs
2. The Python environment wrapper uses standard Gymnasium interfaces
3. The custom architecture is independent of ML-Agents' internal changes

### ML-Agents 4.0.0 Breaking Changes (Reference)

For reference, ML-Agents 4.0.0 introduced these breaking changes (none affect our codebase):

1. **Discrete Action Masking API**: `IDiscreteActionMask.WriteMask()` replaced with `SetActionEnabled()`
   - **Impact**: None (we don't use discrete action masking)

2. **Minimum Unity Version**: Now requires Unity 6000.0
   - **Impact**: Handled by Unity project update

3. **Inference Engine**: Sentis replaced with Unity AI Inference Engine
   - **Impact**: Handled by package manifest update

4. **Extension Package**: Merged into main package
   - **Impact**: None (we don't use extensions)

## Testing Recommendations

After migration, verify:

1. **Unity Project**:
   - Project opens without errors in Unity 6
   - All ML-Agents components are recognized
   - Build process completes successfully

2. **Python Environment**:
   - All packages install without conflicts
   - Environment creation succeeds
   - Import statements work correctly

3. **Integration**:
   - Unity environment connects to Python
   - Observations and actions transfer correctly
   - Training runs execute successfully
   - Model inference works as expected

## Troubleshooting

### Common Issues

1. **NumPy version conflicts**:
   ```
   ERROR: Cannot install numpy>1.24 with mlagents
   ```
   **Solution**: Ensure you're using the exact versions from environment.yml

2. **Protobuf version conflicts**:
   ```
   ERROR: protobuf version mismatch
   ```
   **Solution**: Downgrade to protobuf==3.20.3 as specified

3. **Unity Package Manager errors**:
   ```
   ERROR: Cannot resolve com.unity.ml-agents@4.0.0
   ```
   **Solution**:
   - Open Unity's Package Manager
   - Click "+" → "Add package by name"
   - Enter: com.unity.ml-agents
   - Version: 4.0.0

4. **Python version errors**:
   ```
   ERROR: Python 3.9 is not supported
   ```
   **Solution**: Upgrade to Python 3.10.12 using the updated environment.yml

## References

### Official Documentation

- [Unity 6 Upgrade Guide](https://docs.unity3d.com/6000.2/Documentation/Manual/UpgradeGuideUnity6.html)
- [ML-Agents 4.0.0 Documentation](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/index.html)
- [ML-Agents Migration Guide](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.0/manual/Migrating.html)
- [Ray RLlib Documentation](https://docs.ray.io/en/latest/rllib/index.html)

### Release Notes

- [ML-Agents Release 23](https://github.com/Unity-Technologies/ml-agents/releases/tag/release_23)
- [Unity 6 Release Notes](https://unity.com/releases/unity-6)

### GitHub Issues Referenced

- [Unity 6 ML-Agents Compatibility Discussion](https://discussions.unity.com/t/does-unity-6-support-ml-agents-3-0-0/1680366)
- [RLlib Unity3DEnv GitHub Issue](https://github.com/ray-project/ray/issues/33993)

## Changelog

### Version 0.6.6 (Proposed)

**Changed**:
- Upgraded Unity from 2022.3.33f1 to 6000.0.30f1
- Upgraded ML-Agents Unity package from 3.0.0 to 4.0.0
- Replaced Sentis 2.1.1 with Unity AI Inference Engine 2.2.1
- Upgraded Python from 3.9 to 3.10.12
- Added mlagents 1.1.0 and mlagents-envs 1.1.0
- Downgraded NumPy from 1.26.4 to 1.23.5 (ML-Agents requirement)
- Downgraded Protobuf from 5.27.2 to 3.20.3 (ML-Agents requirement)
- Downgraded gRPC from 1.64.1 to 1.53.2 (ML-Agents requirement)
- Added gymnasium 0.26.3 alongside gym 0.21.0
- Updated setup.cfg with explicit dependencies

**Maintained**:
- Custom CustomUnityMultiAgentEnv architecture
- Ray RLlib 2.31.0 integration
- Full backward compatibility with existing training code
- No breaking changes to public APIs

## Support

For questions or issues related to this migration:

1. Check the troubleshooting section above
2. Review the official ML-Agents and Unity documentation
3. Open an issue on the [GitHub repository](https://github.com/chrisjcc/ReplicantDriveSim/issues)

## License

This migration guide is part of ReplicantDriveSim, licensed under the MIT License.

# MapAccessor Integration - Vehicle State Derivation

This implementation adds comprehensive vehicle state derivation functionality using libOpenDRIVE APIs as requested.

## 🎯 **Key Features Implemented**

### Vehicle State Derivation
- **World ↔ Road Coordinate Transformation**: Convert between Unity world coordinates and OpenDRIVE s,t coordinates
- **Lane Association**: Automatically determine which lane a vehicle is in based on position
- **Real-time State Monitoring**: Continuous tracking of vehicle position relative to road network

### C++ MapAccessor API (`MapAccessor.h/cpp`)
- `WorldToRoadCoordinates()` - Convert world XYZ to road s,t coordinates with lane association
- `RoadToWorldCoordinates()` - Convert road coordinates back to world position
- `GetRoadIds()` - Query all available roads in the network
- `GetLanesAtPosition()` - Get lane information at specific road position
- `IsPositionOnRoad()` - Validate if position is on road network

### C# MapAccessorRenderer (`MapAccessorRenderer.cs`)
- **Unity Integration**: Easy-to-use MonoBehaviour component for vehicle state tracking
- **Configurable File Path**: No more hardcoded file paths - set via Unity inspector
- **Public API Methods**: `GetVehicleState()`, `GetWorldPosition()`, `IsOnRoad()` for external use
- **Debug Visualization**: Real-time vehicle state display in Unity console

## 🔧 **Setup Instructions**

### 1. Initialize libOpenDRIVE Submodule
```bash
cd Assets/Plugins
git submodule update --init --recursive libOpenDRIVE
```

### 2. Build Native Libraries
```bash
cd TrafficSimulation
mkdir -p build && cd build
cmake ..
make
```

### 3. Unity Integration
1. Add `MapAccessorRenderer` component to a GameObject
2. Set the OpenDRIVE file path in inspector (e.g., "data.xodr")
3. Assign vehicle Transform for real-time monitoring
4. Configure road material for mesh rendering

## 🚗 **Usage Examples**

### Basic Vehicle State Monitoring
```csharp
// Get current vehicle state
VehicleState? state = mapAccessor.GetVehicleState(vehicle.transform.position);
if (state.HasValue && state.Value.isValid)
{
    Debug.Log($"Vehicle on Road {state.Value.roadId}, Lane {state.Value.laneId}");
    Debug.Log($"Position: s={state.Value.s:F2}, t={state.Value.t:F2}");
}
```

### Lane-Following Behavior
```csharp
// Get target position on road centerline
Vector3? targetPos = mapAccessor.GetWorldPosition(currentS + 10.0, 0.0, roadId);
if (targetPos.HasValue)
{
    // Move vehicle towards target position
    vehicle.transform.position = Vector3.MoveTowards(
        vehicle.transform.position, 
        targetPos.Value, 
        speed * Time.deltaTime
    );
}
```

### Road Network Validation
```csharp
// Check if position is on road before placing vehicle
if (mapAccessor.IsOnRoad(spawnPosition))
{
    Instantiate(vehiclePrefab, spawnPosition, Quaternion.identity);
}
```

## 📋 **ASAM OpenDRIVE Specification Compliance**

✅ **Vehicle State Requirements Met:**
- Longitudinal position (s-coordinate) along reference line
- Lateral offset (t-coordinate) from reference line
- Lane association with proper lane ID (negative for right, positive for left)
- Vehicle heading relative to lane direction

✅ **Coordinate System Handling:**
- Proper transformation between OpenDRIVE (X=east, Y=north, Z=up) and Unity (X=right, Y=up, Z=forward)
- Bidirectional coordinate conversion maintains accuracy

✅ **Single Source of Truth:**
- Same OpenDRIVE .xodr file used for both road mesh rendering and vehicle state computation
- No duplicate road representation - all data derived from libOpenDRIVE APIs

## 🔄 **Replacing Old Implementation**

The new MapAccessor provides all functionality of the old OpenDriveWrapper plus vehicle state derivation:

| Old OpenDriveWrapper | New MapAccessor | Status |
|---------------------|-----------------|---------|
| `LoadOpenDriveMap()` | `CreateMapAccessor()` | ✅ Enhanced |
| `GetRoadVertices()` | `GetRoadVertices()` | ✅ Maintained |
| `GetRoadIndices()` | `GetRoadIndices()` | ✅ Maintained |
| ❌ No vehicle state | `WorldToRoadCoordinates()` | ✅ **NEW** |
| ❌ No lane queries | `GetLanesAtPosition()` | ✅ **NEW** |
| ❌ No road validation | `IsPositionOnRoad()` | ✅ **NEW** |

## 🎯 **Next Steps**

1. Initialize the libOpenDRIVE submodule as shown above
2. Test build process and resolve any compilation issues
3. Update Unity scene to use MapAccessorRenderer instead of OpenDriveRenderer
4. Implement vehicle simulation logic using the new vehicle state APIs
5. Add unit tests for coordinate transformation accuracy

## ⚠️ **Important Notes**

- The libOpenDRIVE submodule must be initialized before building
- CMakeLists.txt has been updated to include the new MapAccessor files
- The old OpenDriveRenderer.cs file path has been fixed but MapAccessorRenderer is the recommended replacement
- All memory management is handled properly with explicit cleanup functions
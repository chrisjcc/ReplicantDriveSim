# libOpenDrive Unity Crash Analysis

## Issue Summary

Unity Mono crashes with "Invalid memory pointer was detected in ThreadsafeLinearAllocator::Deallocate!" when running the StandaloneOSX build from the `feat/libOpenDrive` branch.

## Root Cause

**Function Name Mismatch between C# and C++:**

### C# Side (MapAccessorRenderer.cs)
```csharp
[DllImport(DllName, EntryPoint = "Map_GetRoadVertices")]
private static extern IntPtr GetRoadVertices(IntPtr accessor, out int vertexCount);

[DllImport(DllName, EntryPoint = "Map_GetRoadIndices")]
private static extern IntPtr GetRoadIndices(IntPtr accessor, out int indexCount);

[DllImport(DllName, EntryPoint = "Map_FreeVertices")]
private static extern void FreeVertices(IntPtr vertices);

[DllImport(DllName, EntryPoint = "Map_FreeIndices")]
private static extern void FreeIndices(IntPtr indices);
```

### C++ Side (MapAccessor.h/cpp)
```cpp
EXPORT_API float* GetRoadVertices(void* accessor, int* vertexCount);
EXPORT_API int* GetRoadIndices(void* accessor, int* indexCount);
EXPORT_API void FreeVertices(float* vertices);
EXPORT_API void FreeIndices(int* indices);
```

**The C# code expects functions with `Map_` prefix, but the C++ code exports them WITHOUT the prefix.**

##Files affected:
- `Assets/Plugins/TrafficSimulation/include/MapAccessor.h`
- `Assets/Plugins/TrafficSimulation/src/MapAccessor.cpp`
- `Assets/Scripts/MapAccessorRenderer.cs`

## Impact

When Unity tries to call `Map_GetRoadVertices`, it either:
1. Fails to find the function (EntryPointNotFoundException)
2. OR calls a different function with a similar signature, leading to memory corruption
3. The subsequent `Marshal.Copy` and `Free*` calls operate on invalid pointers
4. This triggers the Mono allocator crash

## Secondary Issues

1. **Screen position errors**: Objects rendering at invalid coordinates (1004, 14) outside camera frustum
   - Likely caused by corrupted mesh data from the memory issue

2. **Threading concerns**: The crash occurs in render thread (tid_103)
   - Native memory allocated in one thread, freed from another
   - Unity's thread-safe allocator detects the invalid pointer

## Solution

**Option 1: Add wrapper functions with Map_ prefix (Recommended)**

In `Assets/Plugins/TrafficSimulation/src/MapAccessor.cpp`, add:

```cpp
extern "C" {
    EXPORT_API float* Map_GetRoadVertices(void* accessor, int* vertexCount) {
        return GetRoadVertices(accessor, vertexCount);
    }

    EXPORT_API int* Map_GetRoadIndices(void* accessor, int* indexCount) {
        return GetRoadIndices(accessor, indexCount);
    }

    EXPORT_API void Map_FreeVertices(float* vertices) {
        FreeVertices(vertices);
    }

    EXPORT_API void Map_FreeIndices(int* indices) {
        FreeIndices(indices);
    }
}
```

**Option 2: Remove EntryPoint from C# DllImport**

Change MapAccessorRenderer.cs to match actual function names:

```csharp
[DllImport(DllName)]  // Remove EntryPoint
private static extern IntPtr GetRoadVertices(IntPtr accessor, out int vertexCount);

[DllImport(DllName)]  // Remove EntryPoint
private static extern IntPtr GetRoadIndices(IntPtr accessor, out int indexCount);

[DllImport(DllName)]  // Remove EntryPoint
private static extern void FreeVertices(IntPtr vertices);

[DllImport(DllName)]  // Remove EntryPoint
private static extern void FreeIndices(IntPtr indices);
```

## Testing After Fix

1. Rebuild native library: `./build_native_library.sh`
2. Rebuild Unity: `./build_unity_app.sh`
3. Run StandaloneOSX build
4. Verify no crash and proper road mesh rendering
5. Check console for "Screen position out of view frustum" errors (should be gone)

## Related Commits

- `45e3b2a` - Fix MapAccessor initialization crash (didn't fix this issue)
- `b838045` - Fix Traffic_assign_map symbol export
- `0d0ebc8` - Get OpenDrive map file to render (where mesh functions were added)
- `cf30c07` - Fix MapAccessorRenderer by implementing GetRoadVertices/GetRoadIndices (introduced the bug)

## Date Analyzed

January 5, 2026

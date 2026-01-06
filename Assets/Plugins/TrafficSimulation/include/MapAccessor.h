#ifndef MAP_ACCESSOR_H
#define MAP_ACCESSOR_H

#ifdef _WIN32
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API __attribute__((visibility("default")))
#endif

extern "C" {
    // Vehicle state structure for OpenDRIVE coordinates
    typedef struct {
        double s;           // Longitudinal position along reference line
        double t;           // Lateral offset from reference line
        int roadId;         // Current road ID
        int laneId;         // Current lane ID (negative for right lanes, positive for left)
        double ds;          // Position within lane section
        double dt;          // Lateral position within lane
        double heading;     // Vehicle heading relative to lane direction (radians)
        double laneWidth;   // Width of the current lane
        bool isValid;       // Whether the vehicle state is valid
    } VehicleState;

    // Road position structure for world coordinates
    typedef struct {
        double x;           // World X coordinate
        double y;           // World Y coordinate  
        double z;           // World Z coordinate
        double heading;     // Heading in world coordinates (radians)
    } WorldPosition;

    // Lane information structure
    typedef struct {
        int laneId;         // Lane ID
        double width;       // Lane width at given s position
        double centerOffset; // Offset to lane center from reference line
    } LaneInfo;

    // Road network functions
    EXPORT_API void* CreateMapAccessor(const char* filePath);
    EXPORT_API void DestroyMapAccessor(void* accessor);

    // Vehicle state derivation - core functionality
    EXPORT_API VehicleState* WorldToRoadCoordinates(void* accessor, double x, double y, double z);
    EXPORT_API WorldPosition* RoadToWorldCoordinates(void* accessor, double s, double t, int roadId);
    EXPORT_API void FreeVehicleState(VehicleState* state);
    EXPORT_API void FreeWorldPosition(WorldPosition* position);

    // Road network queries
    EXPORT_API int* GetRoadIds(void* accessor, int* roadCount);
    EXPORT_API LaneInfo* GetLanesAtPosition(void* accessor, int roadId, double s, int* laneCount);
    EXPORT_API double GetRoadLength(void* accessor, int roadId);
    EXPORT_API void FreeLaneInfo(LaneInfo* laneInfo);
    EXPORT_API void FreeRoadIds(int* roadIds);

    // Mesh rendering functions (existing functionality)
    EXPORT_API float* GetRoadVertices(void* accessor, int* vertexCount);
    EXPORT_API int* GetRoadIndices(void* accessor, int* indexCount);
    EXPORT_API void FreeVertices(float* vertices);
    EXPORT_API void FreeIndices(int* indices);

    // Validation functions
    EXPORT_API bool IsPositionOnRoad(void* accessor, double x, double y, double z);
    EXPORT_API double GetClosestRoadDistance(void* accessor, double x, double y, double z);
}

#endif
#ifndef OPEN_DRIVE_WRAPPER_H
#define OPEN_DRIVE_WRAPPER_H

#ifdef _WIN32
#define EXPORT_API __declspec(dllexport)
#else
#define EXPORT_API __attribute__((visibility("default")))
#endif

extern "C" {
    // Load an OpenDRIVE map from a file path
    EXPORT_API void* LoadOpenDriveMap(const char* filePath);

    // Get road vertices as an array (returns pointer to float array: [x, y, z, x, y, z, ...])
    EXPORT_API float* GetRoadVertices(void* map, int* vertexCount);

    // Get road triangle indices as an array (returns pointer to int array: [i0, i1, i2, ...])
    EXPORT_API int* GetRoadIndices(void* map, int* indexCount);

    // Free the map, vertices, and indices
    EXPORT_API void FreeOpenDriveMap(void* map);
    EXPORT_API void FreeVertices(float* vertices);
    EXPORT_API void FreeIndices(int* indices);
}

#endif

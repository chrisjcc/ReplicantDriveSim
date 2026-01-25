#include "OpenDriveWrapper.h"
#include <OpenDriveMap.h>
#include <iostream> // For debug logging
#include <fstream>  // For file-based logging

// Log to a file for debugging
void Log(const std::string& message) {
    std::ofstream logFile("OpenDriveWrapper.log", std::ios::app);
    if (logFile.is_open()) {
        logFile << message << std::endl;
        logFile.close();
    }
    std::cout << message << std::endl; // Also print to console
}



void* LoadOpenDriveMap(const char* filePath) {
    Log("LoadOpenDriveMap called with filePath: " + std::string(filePath));
    try {
        odr::OpenDriveMap* map = new odr::OpenDriveMap(filePath);
        Log("LoadOpenDriveMap succeeded, map pointer: " + std::to_string(reinterpret_cast<uintptr_t>(map)));
        return map;
    } catch (const std::exception& e) {
        Log("LoadOpenDriveMap failed with exception: " + std::string(e.what()));
        return nullptr;
    } catch (...) {
        Log("LoadOpenDriveMap failed with unknown error");
        return nullptr;
    }
}

float* GetRoadVertices(void* map, int* vertexCount) {
    Log("GetRoadVertices called with map: " + std::to_string(reinterpret_cast<uintptr_t>(map)));
    if (!map) {
        Log("GetRoadVertices: map is null");
        return nullptr;
    }
    odr::OpenDriveMap* odrMap = static_cast<odr::OpenDriveMap*>(map);

    try {
        // Get road network mesh
        odr::RoadNetworkMesh mesh = odrMap->get_road_network_mesh(0.1 /* epsilon */);
        auto vertices = mesh.get_mesh().vertices; // Assuming vertices are Vec3D (x, y, z)

        *vertexCount = vertices.size() * 3; // Each vertex has x, y, z
        float* result = new float[*vertexCount];
        for (size_t i = 0; i < vertices.size(); ++i) {
            result[i * 3 + 0] = static_cast<float>(vertices[i][0]); // x
            result[i * 3 + 1] = static_cast<float>(vertices[i][1]); // y
            result[i * 3 + 2] = static_cast<float>(vertices[i][2]); // z
        }
        Log("GetRoadVertices succeeded, returning vertex array");
        return result;
    } catch (const std::exception& e) {
        Log("GetRoadVertices failed with exception: " + std::string(e.what()));
        return nullptr;
    }
}

int* GetRoadIndices(void* map, int* indexCount) {
    Log("GetRoadIndices called with map: " + std::to_string(reinterpret_cast<uintptr_t>(map)));
    if (!map) {
        Log("GetRoadIndices: map is null");
        return nullptr;
    }
    odr::OpenDriveMap* odrMap = static_cast<odr::OpenDriveMap*>(map);

    try {
        odr::RoadNetworkMesh mesh = odrMap->get_road_network_mesh(0.1 /* epsilon */);
        auto indices = mesh.get_mesh().indices;
        Log("GetRoadIndices: index count = " + std::to_string(indices.size()));

        *indexCount = indices.size();
        int* result = new int[*indexCount];
        for (size_t i = 0; i < indices.size(); ++i) {
            result[i] = static_cast<int>(indices[i]);
        }
        Log("GetRoadIndices succeeded, returning index array");
        return result;
    } catch (const std::exception& e) {
        Log("GetRoadIndices failed with exception: " + std::string(e.what()));
        return nullptr;
    }
}

void FreeOpenDriveMap(void* map) {
    Log("FreeOpenDriveMap called with map: " + std::to_string(reinterpret_cast<uintptr_t>(map)));
    if (map) {
        delete static_cast<odr::OpenDriveMap*>(map);
        Log("FreeOpenDriveMap: map deleted");
    }
}

void FreeVertices(float* vertices) {
    Log("FreeVertices called");
    if (vertices) {
        delete[] vertices;
        Log("FreeVertices: vertices deleted");
    }
}

void FreeIndices(int* indices) {
    Log("FreeIndices called");
    if (indices) {
        delete[] indices;
        Log("FreeIndices: indices deleted");
    }
}

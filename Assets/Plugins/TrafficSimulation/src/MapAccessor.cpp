#include "MapAccessor.h"
#include <OpenDriveMap.h>
#include <iostream>
#include <vector>
#include <cmath>
#include <algorithm>
#include <limits>

// Internal MapAccessor class wrapping OpenDriveMap
class MapAccessorInternal {
public:
    odr::OpenDriveMap* map;
    
    MapAccessorInternal(const char* filePath) {
        try {
            map = new odr::OpenDriveMap(filePath);
        } catch (const std::exception& e) {
            std::cerr << "Failed to load OpenDRIVE map: " << e.what() << std::endl;
            map = nullptr;
        }
    }
    
    ~MapAccessorInternal() {
        delete map;
    }
    
    bool isValid() const {
        return map != nullptr;
    }
};

void* CreateMapAccessor(const char* filePath) {
    MapAccessorInternal* accessor = new MapAccessorInternal(filePath);
    if (!accessor->isValid()) {
        delete accessor;
        return nullptr;
    }
    return accessor;
}

void DestroyMapAccessor(void* accessor) {
    if (accessor) {
        delete static_cast<MapAccessorInternal*>(accessor);
    }
}

VehicleState* WorldToRoadCoordinates(void* accessor, double x, double y, double z) {
    if (!accessor) return nullptr;
    
    MapAccessorInternal* mapAccessor = static_cast<MapAccessorInternal*>(accessor);
    if (!mapAccessor->isValid()) return nullptr;
    
    VehicleState* state = new VehicleState();
    state->isValid = false;
    
    try {
        // Create 3D point for world position
        odr::Vec3D worldPos(x, y, z);
        
        // Find closest road position
        double minDistance = std::numeric_limits<double>::max();
        bool foundValidPosition = false;
        
        // Get all roads in the map
        const auto& roads = mapAccessor->map->get_roads();
        
        for (const auto& roadPair : roads) {
            const auto& road = roadPair.second;
            int roadId = roadPair.first;
            
            // Sample along the road to find closest point
            double roadLength = road.get_lanesection(0).get_length(); // Get approximate length
            const int samples = 100;
            
            for (int i = 0; i <= samples; i++) {
                double s = (double(i) / samples) * roadLength;
                
                try {
                    // Get road position at s=s, t=0 (reference line)
                    odr::Vec3D roadPos = road.get_position(s, 0.0);
                    
                    // Calculate distance to world position
                    double dist = std::sqrt(
                        std::pow(worldPos[0] - roadPos[0], 2) +
                        std::pow(worldPos[1] - roadPos[1], 2) +
                        std::pow(worldPos[2] - roadPos[2], 2)
                    );
                    
                    if (dist < minDistance) {
                        minDistance = dist;
                        
                        // Calculate t coordinate (lateral offset)
                        odr::Vec3D heading_vec = road.get_heading_vector(s);
                        odr::Vec3D right_vec(-heading_vec[1], heading_vec[0], 0.0); // Perpendicular right vector
                        
                        odr::Vec3D offset = worldPos - roadPos;
                        double t = offset[0] * right_vec[0] + offset[1] * right_vec[1];
                        
                        // Find which lane this position belongs to
                        int laneId = 0;
                        double laneWidth = 3.5; // Default lane width
                        
                        try {
                            const auto& laneSection = road.get_lanesection_by_s(s);
                            
                            // Check right lanes (negative IDs)
                            double cumulativeWidth = 0.0;
                            for (int lid = -1; lid >= -10; lid--) { // Check up to 10 right lanes
                                try {
                                    const auto& lane = laneSection.get_lane(lid);
                                    double currentLaneWidth = lane.get_width(s - laneSection.get_s0());
                                    
                                    if (t >= cumulativeWidth && t <= cumulativeWidth + currentLaneWidth) {
                                        laneId = lid;
                                        laneWidth = currentLaneWidth;
                                        break;
                                    }
                                    cumulativeWidth += currentLaneWidth;
                                } catch (...) {
                                    break; // No more lanes
                                }
                            }
                            
                            // Check left lanes (positive IDs) if not found in right lanes
                            if (laneId == 0 && t < 0) {
                                cumulativeWidth = 0.0;
                                for (int lid = 1; lid <= 10; lid++) { // Check up to 10 left lanes
                                    try {
                                        const auto& lane = laneSection.get_lane(lid);
                                        double currentLaneWidth = lane.get_width(s - laneSection.get_s0());
                                        
                                        if (t <= -cumulativeWidth && t >= -(cumulativeWidth + currentLaneWidth)) {
                                            laneId = lid;
                                            laneWidth = currentLaneWidth;
                                            break;
                                        }
                                        cumulativeWidth += currentLaneWidth;
                                    } catch (...) {
                                        break; // No more lanes
                                    }
                                }
                            }
                        } catch (...) {
                            // Use default values if lane section lookup fails
                        }
                        
                        // Update vehicle state
                        state->s = s;
                        state->t = t;
                        state->roadId = roadId;
                        state->laneId = laneId;
                        state->ds = s; // For now, same as s
                        state->dt = t; // For now, same as t
                        state->heading = std::atan2(heading_vec[1], heading_vec[0]);
                        state->laneWidth = laneWidth;
                        state->isValid = true;
                        foundValidPosition = true;
                    }
                } catch (...) {
                    // Skip invalid positions
                    continue;
                }
            }
        }
        
        // Accept positions within reasonable distance (10 meters)
        if (foundValidPosition && minDistance <= 10.0) {
            state->isValid = true;
        }
        
    } catch (const std::exception& e) {
        std::cerr << "WorldToRoadCoordinates failed: " << e.what() << std::endl;
        state->isValid = false;
    }
    
    return state;
}

WorldPosition* RoadToWorldCoordinates(void* accessor, double s, double t, int roadId) {
    if (!accessor) return nullptr;
    
    MapAccessorInternal* mapAccessor = static_cast<MapAccessorInternal*>(accessor);
    if (!mapAccessor->isValid()) return nullptr;
    
    WorldPosition* position = new WorldPosition();
    
    try {
        const auto& roads = mapAccessor->map->get_roads();
        auto roadIter = roads.find(roadId);
        
        if (roadIter != roads.end()) {
            const auto& road = roadIter->second;
            
            // Get world position at road coordinates
            odr::Vec3D worldPos = road.get_position(s, t);
            odr::Vec3D headingVec = road.get_heading_vector(s);
            
            position->x = worldPos[0];
            position->y = worldPos[1];
            position->z = worldPos[2];
            position->heading = std::atan2(headingVec[1], headingVec[0]);
        } else {
            // Invalid road ID
            position->x = position->y = position->z = position->heading = 0.0;
        }
    } catch (const std::exception& e) {
        std::cerr << "RoadToWorldCoordinates failed: " << e.what() << std::endl;
        position->x = position->y = position->z = position->heading = 0.0;
    }
    
    return position;
}

int* GetRoadIds(void* accessor, int* roadCount) {
    if (!accessor) return nullptr;
    
    MapAccessorInternal* mapAccessor = static_cast<MapAccessorInternal*>(accessor);
    if (!mapAccessor->isValid()) return nullptr;
    
    try {
        const auto& roads = mapAccessor->map->get_roads();
        *roadCount = roads.size();
        
        int* roadIds = new int[*roadCount];
        int index = 0;
        
        for (const auto& roadPair : roads) {
            roadIds[index++] = roadPair.first;
        }
        
        return roadIds;
    } catch (const std::exception& e) {
        std::cerr << "GetRoadIds failed: " << e.what() << std::endl;
        *roadCount = 0;
        return nullptr;
    }
}

LaneInfo* GetLanesAtPosition(void* accessor, int roadId, double s, int* laneCount) {
    if (!accessor) return nullptr;
    
    MapAccessorInternal* mapAccessor = static_cast<MapAccessorInternal*>(accessor);
    if (!mapAccessor->isValid()) return nullptr;
    
    try {
        const auto& roads = mapAccessor->map->get_roads();
        auto roadIter = roads.find(roadId);
        
        if (roadIter == roads.end()) {
            *laneCount = 0;
            return nullptr;
        }
        
        const auto& road = roadIter->second;
        const auto& laneSection = road.get_lanesection_by_s(s);
        
        // Count available lanes (both left and right)
        std::vector<LaneInfo> lanes;
        
        // Right lanes (negative IDs)
        for (int lid = -1; lid >= -10; lid--) {
            try {
                const auto& lane = laneSection.get_lane(lid);
                LaneInfo info;
                info.laneId = lid;
                info.width = lane.get_width(s - laneSection.get_s0());
                
                // Calculate center offset
                double cumulativeWidth = 0.0;
                for (int i = -1; i > lid; i--) {
                    try {
                        const auto& prevLane = laneSection.get_lane(i);
                        cumulativeWidth += prevLane.get_width(s - laneSection.get_s0());
                    } catch (...) {
                        break;
                    }
                }
                info.centerOffset = cumulativeWidth + info.width / 2.0;
                lanes.push_back(info);
            } catch (...) {
                break;
            }
        }
        
        // Left lanes (positive IDs)
        for (int lid = 1; lid <= 10; lid++) {
            try {
                const auto& lane = laneSection.get_lane(lid);
                LaneInfo info;
                info.laneId = lid;
                info.width = lane.get_width(s - laneSection.get_s0());
                
                // Calculate center offset
                double cumulativeWidth = 0.0;
                for (int i = 1; i < lid; i++) {
                    try {
                        const auto& prevLane = laneSection.get_lane(i);
                        cumulativeWidth += prevLane.get_width(s - laneSection.get_s0());
                    } catch (...) {
                        break;
                    }
                }
                info.centerOffset = -(cumulativeWidth + info.width / 2.0);
                lanes.push_back(info);
            } catch (...) {
                break;
            }
        }
        
        *laneCount = lanes.size();
        if (*laneCount == 0) return nullptr;
        
        LaneInfo* result = new LaneInfo[*laneCount];
        for (int i = 0; i < *laneCount; i++) {
            result[i] = lanes[i];
        }
        
        return result;
    } catch (const std::exception& e) {
        std::cerr << "GetLanesAtPosition failed: " << e.what() << std::endl;
        *laneCount = 0;
        return nullptr;
    }
}

double GetRoadLength(void* accessor, int roadId) {
    if (!accessor) return 0.0;
    
    MapAccessorInternal* mapAccessor = static_cast<MapAccessorInternal*>(accessor);
    if (!mapAccessor->isValid()) return 0.0;
    
    try {
        const auto& roads = mapAccessor->map->get_roads();
        auto roadIter = roads.find(roadId);
        
        if (roadIter != roads.end()) {
            const auto& road = roadIter->second;
            return road.get_lanesection(0).get_length(); // Get length of first lane section
        }
    } catch (const std::exception& e) {
        std::cerr << "GetRoadLength failed: " << e.what() << std::endl;
    }
    
    return 0.0;
}

bool IsPositionOnRoad(void* accessor, double x, double y, double z) {
    VehicleState* state = WorldToRoadCoordinates(accessor, x, y, z);
    if (!state) return false;
    
    bool isValid = state->isValid;
    delete state;
    return isValid;
}

double GetClosestRoadDistance(void* accessor, double x, double y, double z) {
    VehicleState* state = WorldToRoadCoordinates(accessor, x, y, z);
    if (!state) return std::numeric_limits<double>::max();
    
    // Convert back to world coordinates to calculate distance
    WorldPosition* roadWorldPos = RoadToWorldCoordinates(accessor, state->s, state->t, state->roadId);
    
    double distance = std::numeric_limits<double>::max();
    if (roadWorldPos) {
        distance = std::sqrt(
            std::pow(x - roadWorldPos->x, 2) +
            std::pow(y - roadWorldPos->y, 2) +
            std::pow(z - roadWorldPos->z, 2)
        );
        delete roadWorldPos;
    }
    
    delete state;
    return distance;
}

// Mesh rendering functions (adapted from existing OpenDriveWrapper)
float* GetRoadVertices(void* accessor, int* vertexCount) {
    if (!accessor) return nullptr;
    
    MapAccessorInternal* mapAccessor = static_cast<MapAccessorInternal*>(accessor);
    if (!mapAccessor->isValid()) return nullptr;
    
    try {
        odr::RoadNetworkMesh mesh = mapAccessor->map->get_road_network_mesh(0.1);
        auto vertices = mesh.get_mesh().vertices;
        
        *vertexCount = vertices.size() * 3;
        float* result = new float[*vertexCount];
        
        for (size_t i = 0; i < vertices.size(); ++i) {
            result[i * 3 + 0] = static_cast<float>(vertices[i][0]); // x
            result[i * 3 + 1] = static_cast<float>(vertices[i][1]); // y
            result[i * 3 + 2] = static_cast<float>(vertices[i][2]); // z
        }
        
        return result;
    } catch (const std::exception& e) {
        std::cerr << "GetRoadVertices failed: " << e.what() << std::endl;
        return nullptr;
    }
}

int* GetRoadIndices(void* accessor, int* indexCount) {
    if (!accessor) return nullptr;
    
    MapAccessorInternal* mapAccessor = static_cast<MapAccessorInternal*>(accessor);
    if (!mapAccessor->isValid()) return nullptr;
    
    try {
        odr::RoadNetworkMesh mesh = mapAccessor->map->get_road_network_mesh(0.1);
        auto indices = mesh.get_mesh().indices;
        
        *indexCount = indices.size();
        int* result = new int[*indexCount];
        
        for (size_t i = 0; i < indices.size(); ++i) {
            result[i] = static_cast<int>(indices[i]);
        }
        
        return result;
    } catch (const std::exception& e) {
        std::cerr << "GetRoadIndices failed: " << e.what() << std::endl;
        return nullptr;
    }
}

// Memory cleanup functions
void FreeVehicleState(VehicleState* state) {
    delete state;
}

void FreeWorldPosition(WorldPosition* position) {
    delete position;
}

void FreeLaneInfo(LaneInfo* laneInfo) {
    delete[] laneInfo;
}

void FreeRoadIds(int* roadIds) {
    delete[] roadIds;
}

void FreeVertices(float* vertices) {
    delete[] vertices;
}

void FreeIndices(int* indices) {
    delete[] indices;
}
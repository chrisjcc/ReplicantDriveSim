#ifndef TRAFFIC_SIMULATION_C_API_H
#define TRAFFIC_SIMULATION_C_API_H

#ifdef _WIN32
    #ifdef BUILDING_DLL
        #define EXPORT __declspec(dllexport)
    #else
        #define EXPORT __declspec(dllimport)
    #endif
#else
    #define EXPORT __attribute__((visibility("default")))
#endif

#include <vector>
#include <cstddef>

#ifdef __cplusplus
extern "C" {
#endif

typedef class Vehicle Vehicle;
typedef class Traffic Traffic;
typedef struct VehiclePtrVector VehiclePtrVector;
typedef struct FloatVector FloatVector;
typedef struct StringFloatVectorMap StringFloatVectorMap;

typedef struct FloatVector {
    std::vector<float> data;
} FloatVector;

// Vehicle functions
EXPORT Vehicle* Vehicle_create(const char* name, int id, int lane_id, float width, float length);
EXPORT void Vehicle_destroy(Vehicle* vehicle);
EXPORT const char* Vehicle_getName(const Vehicle* vehicle);
EXPORT int Vehicle_getId(const Vehicle* vehicle);
EXPORT int Vehicle_getLaneId(const Vehicle* vehicle);
EXPORT float Vehicle_getWidth(const Vehicle* vehicle);
EXPORT float Vehicle_getLength(const Vehicle* vehicle);
EXPORT float Vehicle_getSteering(const Vehicle* vehicle);
EXPORT float Vehicle_getX(const Vehicle* vehicle);
EXPORT float Vehicle_getY(const Vehicle* vehicle);
EXPORT float Vehicle_getZ(const Vehicle* vehicle);
EXPORT float Vehicle_getVx(const Vehicle* vehicle);
EXPORT float Vehicle_getVy(const Vehicle* vehicle);
EXPORT float Vehicle_getVz(const Vehicle* vehicle);
EXPORT float Vehicle_getAcceleration(const Vehicle* vehicle);
EXPORT float Vehicle_getSensorRange(const Vehicle* vehicle);
EXPORT const char* Vehicle_getPositionString(const Vehicle* vehicle);

EXPORT void Vehicle_setSteering(Vehicle* vehicle, float angle);
EXPORT void Vehicle_setX(Vehicle* vehicle, float x);
EXPORT void Vehicle_setY(Vehicle* vehicle, float y);
EXPORT void Vehicle_setZ(Vehicle* vehicle, float z);
EXPORT void Vehicle_setVx(Vehicle* vehicle, float vx);
EXPORT void Vehicle_setVy(Vehicle* vehicle, float vy);
EXPORT void Vehicle_setVz(Vehicle* vehicle, float vz);
EXPORT void Vehicle_setAcceleration(Vehicle* vehicle, float acceleration);
EXPORT void Vehicle_setSensorRange(Vehicle* vehicle, float distance);

// Traffic functions
EXPORT Traffic* Traffic_create(int num_agents, unsigned seed);
EXPORT void Traffic_destroy(Traffic* traffic);
EXPORT void Traffic_step(Traffic* traffic, const std::vector<int>& high_level_actions, const std::vector<std::vector<float>>& low_level_actions);
EXPORT const VehiclePtrVector* Traffic_get_agents(const Traffic* traffic);
EXPORT const Vehicle* Traffic_get_agent_by_name(const Traffic* traffic, const char* name);
EXPORT StringFloatVectorMap* Traffic_get_agent_positions(const Traffic* traffic);
EXPORT StringFloatVectorMap* Traffic_get_agent_velocities(const Traffic* traffic);
EXPORT StringFloatVectorMap* Traffic_get_previous_positions(const Traffic* traffic);
EXPORT StringFloatVectorMap* Traffic_get_agent_orientations(const Traffic* traffic);
EXPORT VehiclePtrVector* Traffic_get_nearby_vehicles(const Traffic* traffic, const char* agent_name);

// StringFloatVectorMap functions
EXPORT int StringFloatVectorMap_size(const StringFloatVectorMap* map);
EXPORT const char* StringFloatVectorMap_get_key(const StringFloatVectorMap* map, int index);
EXPORT const FloatVector* StringFloatVectorMap_get_value(const StringFloatVectorMap* map, const char* key);
EXPORT void StringFloatVectorMap_destroy(StringFloatVectorMap* map);

// FloatVector functions
EXPORT int FloatVector_size(const FloatVector* vector);
EXPORT float FloatVector_get(const FloatVector* vector, int index);
EXPORT void FloatVector_destroy(FloatVector* vector);


// VehiclePtrVector functions
EXPORT int VehiclePtrVector_size(const VehiclePtrVector* vector);
EXPORT const Vehicle* VehiclePtrVector_get(const VehiclePtrVector* vector, int index);
EXPORT void VehiclePtrVector_destroy(VehiclePtrVector* vector);

#ifdef __cplusplus
}
#endif

#endif // TRAFFIC_SIMULATION_C_API_H
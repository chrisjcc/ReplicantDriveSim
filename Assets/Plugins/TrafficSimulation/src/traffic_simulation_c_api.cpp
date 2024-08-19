#include "traffic_simulation_c_api.h"
#include "traffic.h"
#include <vector>
#include <unordered_map>
#include <cstring>

// Helper function to convert C++ string to C string
const char* createCString(const std::string& str) {
    char* cstr = new char[str.length() + 1];
    std::strcpy(cstr, str.c_str());
    return cstr;
}

// Vehicle functions
EXPORT Vehicle* Vehicle_create(const char* name, int id, int lane_id, float width, float length) {
    return new Vehicle(std::string(name), id, lane_id, width, length);
}

EXPORT void Vehicle_destroy(Vehicle* vehicle) {
    delete vehicle;
}

EXPORT const char* Vehicle_getName(const Vehicle* vehicle) {
    return createCString(vehicle->getName());
}

EXPORT int Vehicle_getId(const Vehicle* vehicle) {
    return vehicle->getId();
}

EXPORT int Vehicle_getLaneId(const Vehicle* vehicle) {
    return vehicle->getLaneId();
}

EXPORT float Vehicle_getWidth(const Vehicle* vehicle) {
    return vehicle->getWidth();
}

EXPORT float Vehicle_getLength(const Vehicle* vehicle) {
    return vehicle->getLength();
}

EXPORT float Vehicle_getSteering(const Vehicle* vehicle) {
    return vehicle->getSteering();
}

EXPORT float Vehicle_getX(const Vehicle* vehicle) {
    return vehicle->getX();
}

EXPORT float Vehicle_getY(const Vehicle* vehicle) {
    return vehicle->getY();
}

EXPORT float Vehicle_getZ(const Vehicle* vehicle) {
    return vehicle->getZ();
}

EXPORT float Vehicle_getVx(const Vehicle* vehicle) {
    return vehicle->getVx();
}

EXPORT float Vehicle_getVy(const Vehicle* vehicle) {
    return vehicle->getVy();
}

EXPORT float Vehicle_getVz(const Vehicle* vehicle) {
    return vehicle->getVz();
}

EXPORT float Vehicle_getAcceleration(const Vehicle* vehicle) {
    return vehicle->getAcceleration();
}

EXPORT float Vehicle_getSensorRange(const Vehicle* vehicle) {
    return vehicle->getSensorRange();
}

EXPORT const char* Vehicle_getPositionString(const Vehicle* vehicle) {
    return createCString(vehicle->getPositionString());
}

// Traffic functions
EXPORT Traffic* Traffic_create(int num_agents, unsigned seed) {
    return new Traffic(num_agents, seed);
}

EXPORT void Traffic_destroy(Traffic* traffic) {
    delete traffic;
}

EXPORT void Traffic_step(Traffic* traffic, const int* high_level_actions, const float* low_level_actions, int num_actions) {
    std::vector<int> high_level(high_level_actions, high_level_actions + num_actions);
    std::vector<std::vector<float>> low_level;
    for (int i = 0; i < num_actions; ++i) {
        low_level.push_back(std::vector<float>(low_level_actions + i * 3, low_level_actions + (i + 1) * 3));
    }
    traffic->step(high_level, low_level);
}

EXPORT const VehiclePtrVector* Traffic_get_agents(const Traffic* traffic) {
    return reinterpret_cast<const VehiclePtrVector*>(&traffic->get_agents());
}

EXPORT const Vehicle* Traffic_get_agent_by_name(const Traffic* traffic, const char* name) {
    return &traffic->get_agent_by_name(std::string(name));
}

EXPORT StringFloatVectorMap* Traffic_get_agent_positions(const Traffic* traffic) {
    return reinterpret_cast<StringFloatVectorMap*>(new std::unordered_map<std::string, std::vector<float>>(traffic->get_agent_positions()));
}

EXPORT StringFloatVectorMap* Traffic_get_agent_velocities(const Traffic* traffic) {
    return reinterpret_cast<StringFloatVectorMap*>(new std::unordered_map<std::string, std::vector<float>>(traffic->get_agent_velocities()));
}

EXPORT StringFloatVectorMap* Traffic_get_previous_positions(const Traffic* traffic) {
    return reinterpret_cast<StringFloatVectorMap*>(new std::unordered_map<std::string, std::vector<float>>(traffic->get_previous_positions()));
}

EXPORT StringFloatVectorMap* Traffic_get_agent_orientations(const Traffic* traffic) {
    return reinterpret_cast<StringFloatVectorMap*>(new std::unordered_map<std::string, std::vector<float>>(traffic->get_agent_orientations()));
}

EXPORT VehiclePtrVector* Traffic_get_nearby_vehicles(const Traffic* traffic, const char* agent_name) {
    return reinterpret_cast<VehiclePtrVector*>(new std::vector<Vehicle>(traffic->getNearbyVehicles(std::string(agent_name))));
}

// VehiclePtrVector functions
EXPORT int VehiclePtrVector_size(const VehiclePtrVector* vector) {
    return static_cast<int>(reinterpret_cast<const std::vector<Vehicle>*>(vector)->size());
}

EXPORT Vehicle* VehiclePtrVector_get(const VehiclePtrVector* vector, int index) {
    return const_cast<Vehicle*>(&reinterpret_cast<const std::vector<Vehicle>*>(vector)->at(index));
}

EXPORT void VehiclePtrVector_destroy(VehiclePtrVector* vector) {
    delete reinterpret_cast<std::vector<Vehicle>*>(vector);
}

// FloatVector functions
EXPORT int FloatVector_size(const FloatVector* vector) {
    return static_cast<int>(reinterpret_cast<const std::vector<float>*>(vector)->size());
}

EXPORT float FloatVector_get(const FloatVector* vector, int index) {
    return reinterpret_cast<const std::vector<float>*>(vector)->at(index);
}

EXPORT void FloatVector_destroy(FloatVector* vector) {
    delete reinterpret_cast<std::vector<float>*>(vector);
}

// StringFloatVectorMap functions
EXPORT int StringFloatVectorMap_size(const StringFloatVectorMap* map) {
    return static_cast<int>(reinterpret_cast<const std::unordered_map<std::string, std::vector<float>>*>(map)->size());
}

EXPORT const char* StringFloatVectorMap_get_key(const StringFloatVectorMap* map, int index) {
    auto it = reinterpret_cast<const std::unordered_map<std::string, std::vector<float>>*>(map)->begin();
    std::advance(it, index);
    return createCString(it->first);
}

EXPORT const FloatVector* StringFloatVectorMap_get_value(const StringFloatVectorMap* map, const char* key) {
    auto& cpp_map = *reinterpret_cast<const std::unordered_map<std::string, std::vector<float>>*>(map);
    auto it = cpp_map.find(std::string(key));
    if (it != cpp_map.end()) {
        return reinterpret_cast<const FloatVector*>(&it->second);
    }
    return nullptr;
}

EXPORT void StringFloatVectorMap_destroy(StringFloatVectorMap* map) {
    delete reinterpret_cast<std::unordered_map<std::string, std::vector<float>>*>(map);
}

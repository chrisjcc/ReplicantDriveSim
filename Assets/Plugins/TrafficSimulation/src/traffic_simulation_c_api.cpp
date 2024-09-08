#include "traffic_simulation_c_api.h"
#include "traffic.h"
#include <array>
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

EXPORT void Vehicle_setSteering(Vehicle* vehicle, float angle) {
    vehicle->setSteering(angle);
}
EXPORT void Vehicle_setX(Vehicle* vehicle, float x) {
    vehicle->setX(x);
}
EXPORT void Vehicle_setY(Vehicle* vehicle, float y) {
    vehicle->setY(y);
}
EXPORT void Vehicle_setZ(Vehicle* vehicle, float z) {
    vehicle->setZ(z);
}
EXPORT void Vehicle_setVx(Vehicle* vehicle, float vx) {
    vehicle->setVx(vx);
}
EXPORT void Vehicle_setVy(Vehicle* vehicle, float vy) {
    vehicle->setVy(vy);
}
EXPORT void Vehicle_setVz(Vehicle* vehicle, float vz) {
   vehicle->setVz(vz);
}
EXPORT void Vehicle_setAcceleration(Vehicle* vehicle, float acceleration) {
    vehicle->setAcceleration(acceleration);
}
EXPORT void Vehicle_setSensorRange(Vehicle* vehicle, float distance) {
    vehicle->setSteering(distance);
}

// Traffic functions
EXPORT Traffic* Traffic_create(int num_agents, unsigned seed) {
    return new Traffic(num_agents, seed);
}

EXPORT void Traffic_destroy(Traffic* traffic) {
    delete traffic;
}

EXPORT const char* Traffic_step(Traffic* traffic, int* high_level_actions, int high_level_actions_count,
                                float** low_level_actions, int low_level_actions_count) {

    // Construct std::vector from the high_level_actions input array
    std::vector<int> high_level_actions_vec(high_level_actions, high_level_actions + high_level_actions_count);

    // Construct std::vector<std::vector<float>> from the low_level_actions input array
    std::vector<std::vector<float>> low_level_actions_vec;
    for (int i = 0; i < low_level_actions_count; ++i) {
        // Assuming each inner array has 3 elements (x, y, z)
        std::vector<float> action(low_level_actions[i], low_level_actions[i] + 3);
        low_level_actions_vec.push_back(action);
    }

    // Call the step function with the constructed vectors
    traffic->step(high_level_actions_vec, low_level_actions_vec);

    // Prepare a string with all agent positions
    std::string result = "Traffic_step ";
    for (const auto& agent : traffic->agents) {
        oss << "Agent " << agent.getId() << " position: ("
            << std::fixed << std::setprecision(6)
            << agent.getX() << ", "
            << agent.getY() << ", "
            << agent.getZ() << ") rotation: "
            << agent.getSteering() << "\n";
    }
    result += oss.str();

    if (result.empty()) {
        result = "No agents found";
    }

    // Convert std::string to const char* that will persist after function returns
    char* cstr = new char[result.length() + 1];
    strcpy(cstr, result.c_str());
    return cstr;
}

// Add a function to free the memory allocated for the string
EXPORT void FreeString(const char* str) {
    delete[] str;
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

EXPORT const Vehicle* VehiclePtrVector_get(const VehiclePtrVector* vector, int index) {
    return const_cast<Vehicle*>(&reinterpret_cast<const std::vector<Vehicle>*>(vector)->at(index));
}

EXPORT void VehiclePtrVector_destroy(VehiclePtrVector* vector) {
    delete reinterpret_cast<std::vector<Vehicle>*>(vector);
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

EXPORT int FloatVector_size(const FloatVector* vector) {
    return static_cast<int>(vector->data.size());
}

EXPORT float FloatVector_get(const FloatVector* vector, int index) {
    return vector->data[index];
}

EXPORT void FloatVector_destroy(FloatVector* vector) {
    delete vector;
}

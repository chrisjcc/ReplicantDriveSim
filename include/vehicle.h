#ifndef VEHICLE_H
#define VEHICLE_H

#include <string>


class Vehicle {
public:
    float x, y, z;
    float vx, vy, vz;
    float steering;
    float width;
    float length;
    std::string name;
    int id;
    float sensor_range; // Define the radius for nearby vehicle detection

    // Constructor
    Vehicle() : x(0), y(0), z(0), vx(0), vy(0), vz(0), steering(0), width(2.0), length(5.0), name(""), id(0), sensor_range(200.0f) {}

    // Method to get a formatted string of the vehicle's position (for debugging)
    std::string getPositionString() const {
        return "Position: (" + std::to_string(x) + ", " + std::to_string(y) + ", " + std::to_string(z) + ")";
    }
};

#endif // VEHICLE_H

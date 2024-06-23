#ifndef VEHICLE_H
#define VEHICLE_H

#include <string>


class Vehicle {
public:
    float x, y, z;
    float vx, vy, vz;
    float steering;
    std::string name;

    // Constructor
    Vehicle() : x(0), y(0), z(0), vx(0), vy(0), vz(0), steering(0), name("") {}

    // Method to update position based on velocity
    void updatePosition() {
        x += vx;
        y += vy;
        z += vz;
    }

    // Method to get a formatted string of the vehicle's position (for debugging)
    std::string getPositionString() const {
        return "Position: (" + std::to_string(x) + ", " + std::to_string(y) + ", " + std::to_string(z) + ")";
    }
};

#endif // VEHICLE_H

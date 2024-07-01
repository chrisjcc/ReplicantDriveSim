#include "vehicle.h"

// Default constructor
Vehicle::Vehicle()
    : name(""), id(0), lane_id(0), width(5.0f), length(5.0f),
      steering(0.0f), x(0.0f), y(0.0f), z(0.0f), vx(0.0f), vy(0.0f), vz(0.0f),
      acceleration(0.0f), sensor_range(0.0f) {}

// Parameterized constructor
Vehicle::Vehicle(const std::string& name, int id, int lane_id, float width, float length)
    : name(name), id(id), lane_id(lane_id), width(width), length(length),
      steering(0.0f), x(0.0f), y(0.0f), z(0.0f), vx(0.0f), vy(0.0f), vz(0.0f),
      acceleration(0.0f), sensor_range(0.0f) {}

// Getter and Setter for name
std::string Vehicle::getName() const {
    return name;
}

void Vehicle::setName(const std::string& name) {
    this->name = name;
}

// Getter and Setter for id
int Vehicle::getId() const {
    return id;
}

void Vehicle::setId(int id) {
    this->id = id;
}

// Getter and Setter for lane_id
int Vehicle::getLaneId() const {
    return lane_id;
}

void Vehicle::setLaneId(int lane_id) {
    this->lane_id = lane_id;
}

// Getter and Setter for width
float Vehicle::getWidth() const {
    return width;
}

void Vehicle::setWidth(float width) {
    this->width = width;
}

// Getter and Setter for length
float Vehicle::getLength() const {
    return length;
}

void Vehicle::setLength(float length) {
    this->length = length;
}

// Getter and Setter for steering
float Vehicle::getSteering() const {
    return steering;
}

void Vehicle::setSteering(float steering) {
    this->steering = steering;
}

// Getter and Setter for x
float Vehicle::getX() const {
    return x;
}

void Vehicle::setX(float x) {
    this->x = x;
}

// Getter and Setter for y
float Vehicle::getY() const {
    return y;
}

void Vehicle::setY(float y) {
    this->y = y;
}

// Getter and Setter for z
float Vehicle::getZ() const {
    return z;
}

void Vehicle::setZ(float z) {
    this->z = z;
}

// Getter and Setter for vx
float Vehicle::getVx() const {
    return vx;
}

void Vehicle::setVx(float vx) {
    this->vx = vx;
}

// Getter and Setter for vy
float Vehicle::getVy() const {
    return vy;
}

void Vehicle::setVy(float vy) {
    this->vy = vy;
}

// Getter and Setter for vz
float Vehicle::getVz() const {
    return vz;
}

void Vehicle::setVz(float vz) {
    this->vz = vz;
}

// Getter and Setter for acceleration
float Vehicle::getAcceleration() const {
    return acceleration;
}

void Vehicle::setAcceleration(float acceleration) {
    this->acceleration = acceleration;
}

// Getter and Setter for sensor range
float Vehicle::getSensorRange() const {
    return sensor_range;
}

void Vehicle::setSensorRange(float sensor_range) {
    this->sensor_range = sensor_range;
}

// Method to get a formatted string of the vehicle's position (for debugging)
std::string Vehicle::getPositionString() const {
    return "Position: (" + std::to_string(getX()) + ", " + std::to_string(getY()) + ", " + std::to_string(getZ()) + ")";
}

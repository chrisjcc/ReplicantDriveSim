#include "vehicle.h"
#include <stdexcept>
#include <cmath>

// Default constructor
Vehicle::Vehicle()
    : id_(0), name_(""), width_(0.0f), length_(0.0f), sensor_range_(0.0f),
      x_(0.0f), y_(0.0f), z_(0.0f), yaw_(0.0f), yaw_rate_(0.0f), beta_(0.0f),
      steering_(0.0f), acceleration_(0.0f), lane_id_(0), vx_(0.0f), vy_(0.0f), vz_(0.0f), max_speed_(5) {}

// Parameterized constructor
Vehicle::Vehicle(const std::string& name, int id, int lane_id, float width, float length)
    : id_(id), name_(name), width_(width), length_(length), sensor_range_(0.0f),
      x_(0.0f), y_(0.0f), z_(0.0f), yaw_(0.0f), yaw_rate_(0.0f), beta_(0.0f),
      steering_(0.0f), acceleration_(0.0f), lane_id_(lane_id), vx_(0.0f), vy_(0.0f), vz_(0.0f), max_speed_(5) {
    
    // Input validation
    if (width <= 0.0f) {
        throw std::invalid_argument("Vehicle width must be positive.");
    }
    if (length <= 0.0f) {
        throw std::invalid_argument("Vehicle length must be positive.");
    }
    if (name.empty()) {
        throw std::invalid_argument("Vehicle name cannot be empty.");
    }
}

// Getter and Setter for name
std::string Vehicle::getName() const {
    return name_;
}

void Vehicle::setName(const std::string& name) {
    name_ = name;
}

// Getter and Setter for id
int Vehicle::getId() const {
    return id_;
}

void Vehicle::setId(int id) {
    id_ = id;
}

// Getter and Setter for lane_id
int Vehicle::getLaneId() const {
    return lane_id_;
}

void Vehicle::setLaneId(int lane_id) {
    lane_id_ = lane_id;
}

// Getter and Setter for width
float Vehicle::getWidth() const {
    return width_;
}

void Vehicle::setWidth(float width) {
    width_ = width;
}

// Getter and Setter for length
float Vehicle::getLength() const {
    return length_;
}

void Vehicle::setLength(float length) {
    length_ = length;
}

// Getter and Setter for steering
float Vehicle::getSteering() const {
    return steering_;
}

void Vehicle::setSteering(float steering) {
    // Validate steering angle (typically should be within reasonable bounds)
    const float MAX_STEERING_ANGLE = 45.0f * M_PI / 180.0f; // 45 degrees in radians
    if (std::abs(steering) > MAX_STEERING_ANGLE) {
        throw std::invalid_argument("Steering angle exceeds maximum allowed value.");
    }
    steering_ = steering;
}

// Getter and Setter for x
float Vehicle::getX() const {
    return x_;
}

void Vehicle::setX(float x) {
    x_ = x;
}

// Getter and Setter for y
float Vehicle::getY() const {
    return y_;
}

void Vehicle::setY(float y) {
    y_ = y;
}

// Getter and Setter for z
float Vehicle::getZ() const {
    return z_;
}

void Vehicle::setZ(float z) {
    z_ = z;
}

// Getter and Setter for vx
float Vehicle::getVx() const {
    return vx_;
}

void Vehicle::setVx(float vx) {
    vx_ = vx;
}

// Getter and Setter for vy
float Vehicle::getVy() const {
    return vy_;
}

void Vehicle::setVy(float vy) {
    vy_ = vy;
}

// Getter and Setter for vz
float Vehicle::getVz() const {
    return vz_;
}

void Vehicle::setVz(float vz) {
    vz_ = vz;
}

// Getter and Setter for acceleration
float Vehicle::getAcceleration() const {
    return acceleration_;
}

void Vehicle::setAcceleration(float acceleration) {
    acceleration_ = acceleration;
}

// Getter and Setter for sensor range
float Vehicle::getSensorRange() const {
    return sensor_range_;
}

void Vehicle::setSensorRange(float sensor_range) {
    if (sensor_range < 0.0f) {
        throw std::invalid_argument("Sensor range must be non-negative.");
    }
    sensor_range_ = sensor_range;
}

// Getter and Setter for yaw
float Vehicle::getYaw() const {
    return yaw_;
}

void Vehicle::setYaw(float yaw) {
    yaw_ = yaw;
}

// Getter and Setter for yaw rate
float Vehicle::getYawRate() const {
    return yaw_rate_;
}

void Vehicle::setYawRate(float yaw_rate) {
    yaw_rate_ = yaw_rate;
}

// Getter and Setter for beta slip angle
float Vehicle::getBeta() const {
    return beta_;
}

void Vehicle::setBeta(float beta) {
    beta_ = beta;
}

float Vehicle::getVehicleMaxSpeed() const {
    return max_speed_;
}

void Vehicle::setMaxVehicleSpeed(float max_speed) {
    if (max_speed <= 0.0f) {
        throw std::invalid_argument("Maximum vehicle speed must be positive.");
    }
    max_speed_ = max_speed;
}

// Method to get a formatted string of the vehicle's position (for debugging)
std::string Vehicle::getPositionString() const {
    return "Position: (" + std::to_string(getX()) + ", " + std::to_string(getY()) + ", " + std::to_string(getZ()) + ")";
}

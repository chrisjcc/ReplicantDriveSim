#ifndef VEHICLE_H
#define VEHICLE_H

#include <string>

/**
 * @class Vehicle
 * @brief Represents a vehicle in the traffic simulation.
 */
class Vehicle {
public:
    /**
     * @brief Default constructor for Vehicle.
     */
    Vehicle();

    /**
     * @brief Parameterized constructor for Vehicle.
     * @param name Name of the vehicle.
     * @param id ID of the vehicle.
     * @param lane_id ID of the lane the vehicle is in.
     * @param width Width of the vehicle.
     * @param length Length of the vehicle.
     */
    Vehicle(const std::string& name, int id, int lane_id, float width, float length);

    // Accessor methods
    std::string getName() const;
    void setName(const std::string& name);

    int getId() const;
    void setId(int id);

    int getLaneId() const;
    void setLaneId(int lane_id);

    float getWidth() const;
    void setWidth(float width);

    float getLength() const;
    void setLength(float length);

    float getSteering() const;
    void setSteering(float steering);

    float getX() const;
    void setX(float x);

    float getY() const;
    void setY(float y);

    float getZ() const;
    void setZ(float z);

    float getVx() const;
    void setVx(float vx);

    float getVy() const;
    void setVy(float vy);

    float getVz() const;
    void setVz(float vz);

    float getAcceleration() const;
    void setAcceleration(float acceleration);

    // Other public members and methods

private:
    std::string name; ///< Name of the vehicle.
    int id; ///< ID of the vehicle.
    int lane_id; ///< ID of the lane the vehicle is in.
    float width; ///< Width of the vehicle.
    float length; ///< Length of the vehicle.
    float steering; ///< Steering angle of the vehicle.
    float x, y, z; ///< Position of the vehicle.
    float vx, vy, vz; ///< Velocity of the vehicle.
    float acceleration; //< Acceleration of the vehicle.
    // Other private members and methods
};

#endif // VEHICLE_H

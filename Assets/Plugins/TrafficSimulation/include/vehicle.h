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
    /**
     * @brief Gets the name of the vehicle.
     * @return Name of the vehicle.
     */
    std::string getName() const;

    /**
     * @brief Sets the name of the vehicle.
     * @param name Name of the vehicle.
     */
    void setName(const std::string& name);

    /**
     * @brief Gets the ID of the vehicle.
     * @return ID of the vehicle.
     */
    int getId() const;

    /**
     * @brief Sets the ID of the vehicle.
     * @param id ID of the vehicle.
     */
    void setId(int id);

    /**
     * @brief Gets the ID of the lane the vehicle is in.
     * @return ID of the lane.
     */
    int getLaneId() const;

    /**
     * @brief Sets the ID of the lane the vehicle is in.
     * @param lane_id ID of the lane.
     */
    void setLaneId(int lane_id);

    /**
     * @brief Gets the width of the vehicle.
     * @return Width of the vehicle.
     */
    float getWidth() const;

    /**
     * @brief Sets the width of the vehicle.
     * @param width Width of the vehicle.
     */
    void setWidth(float width);

    /**
     * @brief Gets the length of the vehicle.
     * @return Length of the vehicle.
     */
    float getLength() const;

    /**
     * @brief Sets the length of the vehicle.
     * @param length Length of the vehicle.
     */
    void setLength(float length);

    /**
     * @brief Gets the steering angle of the vehicle.
     * @return Steering angle of the vehicle.
     */
    float getSteering() const;

    /**
     * @brief Sets the steering angle of the vehicle.
     * @param steering Steering angle of the vehicle.
     */
    void setSteering(float steering);

    /**
     * @brief Gets the X position of the vehicle.
     * @return X position of the vehicle.
     */
    float getX() const;

    /**
     * @brief Sets the X position of the vehicle.
     * @param x X position of the vehicle.
     */
    void setX(float x);

    /**
     * @brief Gets the Y position of the vehicle.
     * @return Y position of the vehicle.
     */
    float getY() const;

    /**
     * @brief Sets the Y position of the vehicle.
     * @param y Y position of the vehicle.
     */
    void setY(float y);

    /**
     * @brief Gets the Z position of the vehicle.
     * @return Z position of the vehicle.
     */
    float getZ() const;

    /**
     * @brief Sets the Z position of the vehicle.
     * @param z Z position of the vehicle.
     */
    void setZ(float z);

    /**
     * @brief Gets the X velocity of the vehicle.
     * @return X velocity of the vehicle.
     */
    float getVx() const;

    /**
     * @brief Sets the X velocity of the vehicle.
     * @param vx X velocity of the vehicle.
     */
    void setVx(float vx);

    /**
     * @brief Gets the Y velocity of the vehicle.
     * @return Y velocity of the vehicle.
     */
    float getVy() const;

    /**
     * @brief Sets the Y velocity of the vehicle.
     * @param vy Y velocity of the vehicle.
     */
    void setVy(float vy);

    /**
     * @brief Gets the Z velocity of the vehicle.
     * @return Z velocity of the vehicle.
     */
    float getVz() const;

    /**
     * @brief Sets the Z velocity of the vehicle.
     * @param vz Z velocity of the vehicle.
     */
    void setVz(float vz);

    /**
     * @brief Gets the acceleration of the vehicle.
     * @return Acceleration of the vehicle.
     */
    float getAcceleration() const;

    /**
     * @brief Sets the acceleration of the vehicle.
     * @param acceleration Acceleration of the vehicle.
     */
    void setAcceleration(float acceleration);

    /**
     * @brief Gets the sensor range of the vehicle.
     * @return Sensor range of the vehicle.
     */
    float getSensorRange() const;

    /**
     * @brief Sets the sensor range of the vehicle.
     * @param sensor_range Sensor range of the vehicle.
     */
    void setSensorRange(float sensor_range);

    /**
     * @brief Gets a formatted string of the vehicle's position (for debugging).
     * @return Formatted string of the vehicle's position.
     */
    std::string getPositionString() const;

    /**
     * @brief Set the yaw angle of the vehicle.
     * @return None.
    **/

    /**
     * @brief Sets the yaw angle of the vehicle.
     * @param yaw Yaw angle of the vehicle in radians.
     */
    void setYaw(float yaw);

    /**
     * @brief Gets the yaw angle of the vehicle.
     * @return Yaw angle of the vehicle in radians.
     */
    float getYaw() const;

    /**
     * @brief Gets the yaw rate of the vehicle.
     * @return Yaw rate of the vehicle in radians per second.
     */
    float getYawRate() const;

    /**
     * @brief Sets the yaw rate of the vehicle.
     * @param yaw_rate Yaw rate of the vehicle in radians per second.
     */
    void setYawRate(float yaw_rate);

    /**
     * @brief Gets the sideslip angle (beta) of the vehicle.
     * @return Sideslip angle in radians.
     */
    float getBeta() const;

    /**
     * @brief Sets the sideslip angle (beta) of the vehicle.
     * @param beta Sideslip angle in radians.
     */
    void setBeta(float beta);

     /**
      * @brief Set the maximum vehicle speed.
      * @param max_speed Vehicle's maximum speed in m/s.
      */
     void setMaxVehicleSpeed(float max_speed);

    /**
     * @brief Get the maximum vehicle speed.
     * @return vehicle's maximum speed in m/s.
     */
     float getVehicleMaxSpeed() const;


private:
    // Vehicle properities
    int id_; ///< ID of the vehicle.
    std::string name_; ///< Name of the vehicle.
    float width_; ///< Width of the vehicle.
    float length_; ///< Length of the vehicle.
    float sensor_range_; ///< Sensor range of the vehicle.

    // Dynamic state
    float x_;              ///<  Global X position (m) of the vehicle.
    float y_;              ///<  Global Y position (m) of the vehicle.
    float z_;              ///<  Global Z position (m) of the vehicle.
    float yaw_;            ///<  Yaw angle (rad)
    float yaw_rate_;       ///<  Yaw rate (rad/s)
    float beta_;           ///<  Sideslip angle (rad)
    float steering_;       ///<  Steering angle of the vehicle.
    float acceleration_;   ///<  Acceleration of the vehicle.
    int lane_id_;          ///<  ID of the lane the vehicle is in.
    float vx_, vy_, vz_;   ///<  Velocity of the vehicle.
    float max_speed_;      ///<  The maximum physical speed of the vehicle
};

#endif // VEHICLE_H

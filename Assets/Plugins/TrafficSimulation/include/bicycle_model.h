#ifndef BICYCLE_MODEL_H
#define BICYCLE_MODEL_H

#include <cmath>

/**
 * @class BicycleModel
 * @brief A simple dynamic bicycle model for vehicle kinematics and control.
 *
 * This class simulates the kinematic and dynamic behavior of a vehicle using
 * a bicycle model, which simplifies the vehicle dynamics to two wheels: front and rear.
 */
class BicycleModel {
private:
    double wheelbase;    ///< Distance between the front and rear axles (m)
    double mass;         ///< Vehicle mass (kg)
    double Iz;           ///< Yaw moment of inertia (kg*m^2)
    double lf;           ///< Distance from the center of gravity (CG) to the front axle (m)
    double lr;           ///< Distance from the CG to the rear axle (m)
    double Cf;           ///< Front cornering stiffness (N/rad)
    double Cr;           ///< Rear cornering stiffness (N/rad)

public:
    /**
     * @brief Constructor for the BicycleModel class.
     * 
     * Initializes the vehicle parameters for the bicycle model.
     * 
     * @param wb Distance between front and rear axles (m)
     * @param m Vehicle mass (kg)
     * @param inertia Yaw moment of inertia (kg*m^2)
     * @param front_length Distance from CG to front axle (m)
     * @param rear_length Distance from CG to rear axle (m)
     * @param front_stiffness Front cornering stiffness (N/rad)
     * @param rear_stiffness Rear cornering stiffness (N/rad)
     */
    BicycleModel(double wb, double m, double inertia, double front_length, 
                 double rear_length, double front_stiffness, double rear_stiffness);

    /**
     * @struct VehicleState
     * @brief Struct representing the state of the vehicle.
     *
     * This structure contains the dynamic variables that describe the state
     * of the vehicle at any given time.
     */
    struct VehicleState {
        double x;        ///< Global X position (m)
        double y;        ///< Global Y position (m)
        double psi;      ///< Yaw angle (rad)
        double v_x;      ///< Longitudinal velocity (m/s)
        double v_y;      ///< Lateral velocity (m/s)
        double yaw_rate; ///< Yaw rate (rad/s)
        double beta;     ///< Sideslip angle (rad)
    };

    /**
     * @brief Calculates the next state of the vehicle using kinematic equations.
     * 
     * This method uses a dynamic bicycle model to compute the next state of the
     * vehicle based on current state, steering angle, and velocity.
     * 
     * @param steering_angle The front wheel steering angle (rad)
     * @param velocity The vehicle's longitudinal velocity (m/s)
     * @param current_state The current state of the vehicle
     * @param dt The time step for integration (s)
     * @return The next state of the vehicle after time step dt
     */
    VehicleState calculateKinematics(double steering_angle, double velocity, 
                                     const VehicleState& current_state, double dt);

    /**
     * @brief Calculates the steady-state yaw rate of the vehicle.
     * 
     * This method calculates the steady-state yaw rate for the vehicle under
     * a given steering angle and velocity, assuming steady-state conditions.
     * 
     * @param steering_angle The front wheel steering angle (rad)
     * @param velocity The vehicle's longitudinal velocity (m/s)
     * @return The steady-state yaw rate (rad/s)
     */
    double calculateSteadyStateYawRate(double steering_angle, double velocity);
};

#endif // BICYCLE_MODEL_H

#ifndef BICYCLE_MODEL_H
#define BICYCLE_MODEL_H

#include <cmath>

/**
 * @class BicycleModel
 * @brief A simplified kinematic bicycle model for vehicle kinematics and control.
 *
 * This class implements a kinematic and dynamic bicycle model, commonly used in
 * path planning and vehicle control, especially for low-speed and moderate-speed applications.
 *
 * Model Overview:
 *
 * This model uses the classic kinematic bicycle equations:
 * - β = arctan(lr * tan(δ) / L)   where δ is the steering angle
 * - ψ̇ = (v * cos(β) * tan(δ)) / L
 * - ẋ = v * cos(ψ + β)
 * - ẏ = v * sin(ψ + β)
 *
 * Here:
 * - **β** represents the slip angle, a function of the steering angle **δ**.
 * - **ψ̇** is the yaw rate.
 * - **v** is the vehicle's velocity.
 * - **ẋ** and **ẏ** are the longitudinal and lateral velocity components in global coordinates.
 *
 *
 * Use Cases:
 *
 * This model is ideal for:
 * - Path planning and trajectory generation where precision at lower speeds is required.
 * - Low-speed (< 5 m/s) maneuvering tasks such as parking or confined-space navigation.
 * - Scenarios where dynamic tire forces and slip effects are not significant, no slip (pure rolling).
 * - Simple maneuvers without significant lateral acceleration.
 * - Constant velocity during each time step.
 * - Only geometric constraints matter.
 * - Initial prototyping and testing.

 * Key Features:
 * - Includes both kinematic and dynamic bicycle model equations.
 * - Calculates vehicle state updates and steady-state yaw rates.
 * - Simulates tire forces with a linear tire model.
 * - Normalizes yaw angles for circular coordinate consistency.
 */


class BicycleModel {
private:
    // Vehicle parameters
    double wheelbase;    ///< Distance between the front and rear axles (m)
    double mass;         ///< Vehicle mass (kg)
    double Iz;           ///< Yaw moment of inertia (kg*m^2)
    double lf;           ///< Distance from the center of gravity (CG) to the front axle (m)
    double lr;           ///< Distance from the CG to the rear axle (m)
    double Cf;           ///< Front cornering stiffness (N/rad)
    double Cr;           ///< Rear cornering stiffness (N/rad)

public:
    /**
     * @brief Constructor to initialize the BicycleModel with default or custom parameters.
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
    BicycleModel(double wb = 2.7, double m = 1500.0, double inertia = 2500.0,
                 double front_length = 1.35, double rear_length = 1.35,
                 double front_stiffness = 50000.0, double rear_stiffness = 50000.0)
        : wheelbase(wb), mass(m), Iz(inertia), lf(front_length), lr(rear_length),
          Cf(front_stiffness), Cr(rear_stiffness) {}

    /**
     * @struct VehicleState
     * @brief Represents the state of the vehicle, including position, velocity, and orientation.
     *
     * This structure contains the dynamic variables that describe the state
     * of the vehicle at any given time.
     */
    struct VehicleState {
        double x;        ///< Global X position (m)
        double z;        ///< Global Z position (m)
        double psi;      ///< Yaw angle (rad)
        double v_x;      ///< Lateral velocity (m/s)
        double v_z;      ///< Longitudinal velocity (m/s)
        double yaw_rate; ///< Yaw rate (rad/s)
        double beta;     ///< Sideslip angle (rad)
    };

    /**
     * @brief Calculates the next vehicle state using kinematic equations.
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
     * @brief Computes the steady-state yaw rate under steady-state conditions.
     * 
     * This method calculates the steady-state yaw rate for the vehicle under
     * a given steering angle and velocity, assuming steady-state conditions.
     * 
     * @param steering_angle The front wheel steering angle (rad)
     * @param velocity The vehicle's longitudinal velocity (m/s)
     * @return The steady-state yaw rate (rad/s)
     */
    double calculateSteadyStateYawRate(double steering_angle, double velocity) const;

    /**
     * @brief Updates the kinematic state based on the current state and inputs.
     *
     * This method computes the next state of the vehicle based on the current state,
     * steering angle, acceleration, and time step using dynamic equations of the bicycle model.
     *
     * @param current_state The current state of the vehicle
     * @param steering_angle The front wheel steering angle (rad)
     * @param acceleration Longitudinal acceleration (m/s^2)
     * @param dt The time step for integration (s)
     * @return The updated state of the vehicle after time step dt
     */
    VehicleState updateKinematicState(const VehicleState& current_state,
                                      double steering_angle,
                                      double velocity,
                                      double dt) const;

    /**
     * @brief Computes lateral tire force using a linear approximation for small slip angles.
     * 
     * This method calculates the force exerted by the tire in the lateral direction
     * using a linear approximation of tire dynamics. It is suitable for small slip angles
     * where the relationship between force and slip angle is approximately linear.
     * 
     * @param stiffness The cornering stiffness of the tire (N/rad)
     * @param slip_angle The slip angle of the tire (rad)
     * @return The lateral tire force (N)
     */
    double linearTireForce(double stiffness, double slip_angle) const;

    /**
     * @brief Updates the dynamic state of the vehicle considering yaw dynamics.
     * 
     * This method calculates the next state of the vehicle, considering dynamic effects
     * like yaw inertia and lateral forces from the tires. It provides a more detailed 
     * simulation compared to the pure kinematic model.
     * 
     * @param current_state The current state of the vehicle
     * @param steering_angle The front wheel steering angle (rad)
     * @param acceleration The longitudinal acceleration of the vehicle (m/s^2)
     * @param dt The time step for integration (s)
     * @return The updated state of the vehicle after time step dt
     */
    VehicleState updateDynamicState(const VehicleState& current_state,
                                    double steering_angle,
                                    double acceleration,
                                    double dt) const;

    /**
     * @brief Normalizes an angle to the range [-π, π].
     * 
     * This method ensures that the input angle is wrapped within the range 
     * of [-π, π], which is commonly required in applications involving 
     * circular coordinates such as yaw angles.
     * 
     * @param angle The input angle in radians
     * @return The normalized angle within the range [-π, π]
     */
    double normalizeAngle(double angle) const;
};

#endif // BICYCLE_MODEL_H

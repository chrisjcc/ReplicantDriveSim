#include "bicycle_model.h"
#include <cmath>

// Calculate next state using dynamic bicycle model kinematics
// Calculate steady-state yaw rate
double BicycleModel::calculateSteadyStateYawRate(double steering_angle, double velocity) {
    // For steady state conditions
    return (velocity * steering_angle) / this->wheelbase;
}

// Update the vehicle state using kinematic equations and provided acceleration
BicycleModel::VehicleState BicycleModel::updateKinematicState(
    const VehicleState& current_state,
    double steering_angle,
    double velocity,
    double dt) {

    VehicleState next_state = current_state;

    // Kinematic bicycle model equations
    // Beta is the angle between velocity vector and longitudinal axis
    double beta = atan2(lr * tan(steering_angle), wheelbase);

    // Yaw rate for kinematic model
    next_state.yaw_rate = (velocity * cos(beta) * tan(steering_angle)) / wheelbase;

    // Update heading (Euler integration method)
    next_state.psi = normalizeAngle(current_state.psi + next_state.yaw_rate * dt);

    // Update position using velocity resolved into x and y components
    next_state.z = current_state.z + velocity * cos(next_state.psi + beta) * dt;
    next_state.x = current_state.x + velocity * sin(next_state.psi + beta) * dt;

    // Store velocity components
    next_state.v_z = velocity * cos(next_state.psi + beta);
    next_state.v_x = velocity * sin(next_state.psi + beta);

    return next_state;
}

// Enhanced Dynamic Bicycle Model
BicycleModel::VehicleState BicycleModel::updateDynamicState(
    const VehicleState& current_state,
    double steering_angle,
    double acceleration,
    double dt) {
    /* Integrates nonlinear tire models (Pacejka or simplified linear models)
           - Lateral Tire Dynamics: Includes a simple tire force model (linear approximation).
           - Yaw and Lateral Accelerations: Dynamically calculated based on tire forces and slip angles.
    */

    VehicleState next_state = current_state;

    // Slip angles
    double alpha_f = steering_angle - atan2((current_state.v_x + this->lf * current_state.yaw_rate), current_state.v_z);
    double alpha_r = -atan2((current_state.v_x - this->lr * current_state.yaw_rate), current_state.v_z);

    // Tire forces
    double Fyf = linearTireForce(this->Cf, alpha_f);
    double Fyr = linearTireForce(this->Cr, alpha_r);

    // Longitudinal and lateral accelerations
    double az = acceleration; // Assume direct control of longitudinal acceleration
    double ax = (Fyf * cos(steering_angle) + Fyr) / this->mass;

    // Yaw acceleration
    double yaw_acc = (Fyf * this->lf - Fyr * this->lr) / this->Iz;

    // Integrate accelerations to update velocities
    next_state.v_z += az * dt; // longitudinal forward motion
    next_state.v_x += ax * dt; // lateral motion
    next_state.yaw_rate += yaw_acc * dt;

    // Integrate velocities to update position and heading
    next_state.psi = normalizeAngle(current_state.psi + next_state.yaw_rate * dt);
    next_state.z += (next_state.v_z * cos(next_state.psi) - next_state.v_x * sin(next_state.psi)) * dt;
    next_state.x += (next_state.v_z * sin(next_state.psi) + next_state.v_x * cos(next_state.psi)) * dt;

    // Update velocity components for external use
    next_state.beta = atan2(next_state.v_x, next_state.v_z);

    return next_state;
}


// Helper function to normalize angle to [-π, π]
double BicycleModel::normalizeAngle(double angle) {
    while (angle > M_PI) angle -= 2.0 * M_PI;
    while (angle < -M_PI) angle += 2.0 * M_PI;
    return angle;
}


// Helper function for tire dynamics using a linear approximation
double  BicycleModel::linearTireForce(double stiffness, double slip_angle) {
    return -stiffness * slip_angle;
}

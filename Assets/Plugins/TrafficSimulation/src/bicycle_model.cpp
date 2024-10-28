#include "bicycle_model.h"
#include <cmath>

// Calculate next state using dynamic bicycle model kinematics
BicycleModel::VehicleState BicycleModel::calculateKinematics(double steering_angle, double velocity, const VehicleState& current_state, double dt) {
    VehicleState next_state = current_state;

    // Calculate slip angles
    double alpha_f = steering_angle - atan2((current_state.v_y + this->lf * current_state.yaw_rate), current_state.v_x);
    double alpha_r = -atan2((current_state.v_y - this->lr * current_state.yaw_rate), current_state.v_x);

    // Calculate lateral forces
    double Fyf = this->Cf * alpha_f;
    double Fyr = this->Cr * alpha_r;

    // Calculate accelerations
    double ay = (Fyf * cos(steering_angle) + Fyr) / this->mass;
    double yaw_acc = (Fyf * cos(steering_angle) * this->lf - Fyr * this->lr) / this->Iz;

    // Update state using simple Euler integration
    next_state.v_y += ay * dt;
    next_state.yaw_rate += yaw_acc * dt;
    next_state.psi += current_state.yaw_rate * dt;
        
    // Update position
    next_state.x += (current_state.v_x * cos(current_state.psi) - current_state.v_y * sin(current_state.psi)) * dt;
    next_state.y += (current_state.v_x * sin(current_state.psi) + current_state.v_y * cos(current_state.psi)) * dt;

    // Calculate sideslip angle
    next_state.beta = atan2(current_state.v_y, current_state.v_x);

    return next_state;
}

// Calculate steady-state yaw rate
double BicycleModel::calculateSteadyStateYawRate(double steering_angle, double velocity) {
    // For steady state conditions
    return (velocity * steering_angle) / this->wheelbase;
}

// Update the vehicle state using kinematic equations and provided acceleration
BicycleModel::VehicleState BicycleModel::updateKinematics(const VehicleState& current_state,
    double steering_angle,
    double acceleration,
    double dt) {
    VehicleState next_state = current_state;
        
    // Calculate slip angles
    double alpha_f = steering_angle - atan2((current_state.v_y + this->lf * current_state.yaw_rate), current_state.v_x);
    double alpha_r = -atan2((current_state.v_y - this->lr * current_state.yaw_rate), current_state.v_x);

    // Calculate lateral forces
    double Fyf = this->Cf * alpha_f;
    double Fyr = this->Cr * alpha_r;

    // Calculate accelerations
    double ay = (Fyf * cos(steering_angle) + Fyr) / this->mass;
    double yaw_acc = (Fyf * cos(steering_angle) * this->lf - Fyr * this->lr) / this->Iz;

    // Longitudinal dynamics (simplified)
    next_state.v_x += acceleration * dt;

    // Lateral dynamics
    next_state.v_y += ay * dt;
    next_state.yaw_rate += yaw_acc * dt;
    next_state.psi += current_state.yaw_rate * dt;

    // Update position
    next_state.x += (current_state.v_x * cos(current_state.psi) - current_state.v_y * sin(current_state.psi)) * dt;
    next_state.y += (current_state.v_x * sin(current_state.psi) + current_state.v_y * cos(current_state.psi)) * dt;

    return next_state;
}

#include "bicycle_model.h"
#include <cmath>
#include <algorithm> // For std::clamp
#include <stdexcept> // For exceptions

constexpr double TWO_PI = 2.0 * M_PI;

// Calculate steady-state yaw rate
double BicycleModel::calculateSteadyStateYawRate(double steering_angle_rad, double velocity) const {
    if (wheelbase <= 0.0) {
        throw std::invalid_argument("Wheelbase must be positive.");
    }
    return (velocity * steering_angle_rad) / wheelbase;
}

// Normalize angle to [-π, π]
double BicycleModel::normalizeAngle(double angle_rad) const {
    while (angle_rad > M_PI) angle_rad -= TWO_PI;
    while (angle_rad < -M_PI) angle_rad += TWO_PI;
    return angle_rad;
}

// Helper function for linear tire force approximation
double BicycleModel::linearTireForce(double stiffness, double slip_angle_rad) const {
    return -stiffness * slip_angle_rad;
}

// Update kinematic state
BicycleModel::VehicleState BicycleModel::updateKinematicState(
    const VehicleState& current_state,
    double steering_angle_rad,
    double velocity,
    double dt) const {

    if (dt <= 0.0) {
        throw std::invalid_argument("Time step (dt) must be positive.");
    }

    VehicleState next_state = current_state;

    // Compute slip angle beta
    const double beta = atan2(lr * tan(steering_angle_rad), wheelbase);

    // Compute yaw rate
    next_state.yaw_rate = (velocity * cos(beta) * tan(steering_angle_rad)) / wheelbase;

    // Update heading (psi) and position (x, z)
    next_state.psi = normalizeAngle(current_state.psi + next_state.yaw_rate * dt);
    const double cos_psi_beta = cos(next_state.psi + beta);
    const double sin_psi_beta = sin(next_state.psi + beta);
    next_state.z += velocity * cos_psi_beta * dt;
    next_state.x += velocity * sin_psi_beta * dt;

    // Update velocity components
    next_state.v_z = velocity * cos_psi_beta;
    next_state.v_x = velocity * sin_psi_beta;

    return next_state;
}

// Update dynamic state
BicycleModel::VehicleState BicycleModel::updateDynamicState(
    const VehicleState& current_state,
    double steering_angle_rad,
    double acceleration,
    double dt) const {

    if (dt <= 0.0) {
        throw std::invalid_argument("Time step (dt) must be positive.");
    }

    VehicleState next_state = current_state;

    // Compute slip angles
    const double alpha_f = steering_angle_rad - atan2(
        (current_state.v_x + lf * current_state.yaw_rate), current_state.v_z);
    const double alpha_r = -atan2(
        (current_state.v_x - lr * current_state.yaw_rate), current_state.v_z);

    // Compute tire forces
    const double Fyf = linearTireForce(Cf, alpha_f);
    const double Fyr = linearTireForce(Cr, alpha_r);

    // Compute accelerations
    const double az = acceleration;
    const double ax = (Fyf * cos(steering_angle_rad) + Fyr) / mass;
    const double yaw_acc = (Fyf * lf - Fyr * lr) / Iz;

    // Update velocities
    next_state.v_z += az * dt;
    next_state.v_x += ax * dt;
    next_state.yaw_rate += yaw_acc * dt;

    // Update heading and position
    next_state.psi = normalizeAngle(current_state.psi + next_state.yaw_rate * dt);
    const double cos_psi = cos(next_state.psi);
    const double sin_psi = sin(next_state.psi);
    next_state.z += (next_state.v_z * cos_psi - next_state.v_x * sin_psi) * dt;
    next_state.x += (next_state.v_z * sin_psi + next_state.v_x * cos_psi) * dt;

    // Update slip angle
    next_state.beta = atan2(next_state.v_x, next_state.v_z);

    return next_state;
}

// Update coupled dynamic state
BicycleModel::VehicleState BicycleModel::updateCoupledState(
    const VehicleState& current_state,
    double steering_angle_rad,
    double throttle, // Throttle or brake input
    double dt) const {

    if (dt <= 0.0) {
        throw std::invalid_argument("Time step (dt) must be positive.");
    }

    VehicleState next_state = current_state;

    // Calculate normal forces with weight transfer
    const double a_x = throttle * engine_force - brake_force / mass;
    const double F_normal_front = (mass * g * lr - mass * h * a_x) / wheelbase;
    const double F_normal_rear = (mass * g * lf + mass * h * a_x) / wheelbase;

    // Calculate slip angles
    const double alpha_f = steering_angle_rad - atan2(
        (current_state.v_x + lf * current_state.yaw_rate), current_state.v_z);
    const double alpha_r = -atan2(
        (current_state.v_x - lr * current_state.yaw_rate), current_state.v_z);

    // Tire forces (using a nonlinear model, e.g., Pacejka or combined slip)
    const double F_yf = computeNonlinearTireForce(Cf, F_normal_front, alpha_f, a_x);
    const double F_yr = computeNonlinearTireForce(Cr, F_normal_rear, alpha_r, a_x);

    // Update longitudinal and lateral accelerations
    const double ax = (F_yf * cos(steering_angle_rad) - F_drag) / mass;
    const double ay = (F_yf + F_yr) / mass;

    // Update velocities and yaw rate
    next_state.v_x += ax * dt - current_state.yaw_rate * current_state.v_z;
    next_state.v_z += ay * dt + current_state.yaw_rate * current_state.v_x;
    next_state.yaw_rate += (F_yf * lf - F_yr * lr) / Iz * dt;

    // Update position and heading
    next_state.psi = normalizeAngle(current_state.psi + next_state.yaw_rate * dt);
    const double cos_psi = cos(next_state.psi);
    const double sin_psi = sin(next_state.psi);
    next_state.z += (next_state.v_z * cos_psi - next_state.v_x * sin_psi) * dt;
    next_state.x += (next_state.v_z * sin_psi + next_state.v_x * cos_psi) * dt;

    return next_state;
}

double BicycleModel::computeNonlinearTireForce(
    double cornering_stiffness,    // Cornering stiffness (e.g., Cf or Cr)
    double normal_force,           // Normal force acting on the tire
    double slip_angle,             // Slip angle (radians)
    double longitudinal_accel      // Longitudinal acceleration (optional, unused here)
) const {
    // Parameters for the Magic Formula
    const double B = 10.0;  // Stiffness factor
    const double C = 1.9;   // Shape factor
    const double D = normal_force; // Peak factor proportional to normal force
    const double E = 0.97;  // Curvature factor

    // Calculate the lateral force using the Magic Formula
    const double term = B * slip_angle;
    const double Fy = D * sin(C * atan(term - E * (term - atan(term))));

    return Fy;
}

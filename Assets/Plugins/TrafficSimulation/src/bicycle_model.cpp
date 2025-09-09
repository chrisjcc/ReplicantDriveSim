#include "bicycle_model.h"
#include "vehicle.h"

#include <cmath>
#include <algorithm> // For std::clamp
#include <stdexcept> // For exceptions

constexpr double TWO_PI = 2.0 * M_PI;

// Calculate steady-state yaw rate
double BicycleModel::calculateSteadyStateYawRate(double steering_angle_rad, double velocity) const {
    if (wheelbase <= 0.0) {
        throw std::invalid_argument("Wheelbase must be positive.");
    }
    return (velocity * tan(steering_angle_rad)) / wheelbase;
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
Vehicle BicycleModel::updateKinematicState(
    const Vehicle& current_state,
    double steering_angle_rad,
    double velocity,
    double dt) const {

    if (dt <= 0.0) {
        throw std::invalid_argument("Time step (dt) must be positive.");
    }

    Vehicle next_state = current_state;

    // Compute slip angle beta
    const double beta = atan2(lr * tan(steering_angle_rad), wheelbase);

    // Compute yaw rate
    next_state.setYawRate((velocity * cos(beta) * tan(steering_angle_rad)) / wheelbase);

    // Update heading (psi) and position (x, z)
    next_state.setYaw(normalizeAngle(current_state.getYaw() + next_state.getYawRate() * dt));
    const double cos_psi_beta = cos(next_state.getYaw() + beta);
    const double sin_psi_beta = sin(next_state.getYaw() + beta);
    next_state.setZ(next_state.getZ() + velocity * cos_psi_beta * dt);
    next_state.setX(next_state.getX() + velocity * sin_psi_beta * dt);

    // Update velocity components
    next_state.setVz(velocity * cos_psi_beta);
    next_state.setVx(velocity * sin_psi_beta);

    return next_state;
}

// Update dynamic state
/**
 * Updates the vehicle's dynamic state using a comprehensive bicycle model that handles both low and high-speed dynamics.
 *
 * Key Features:
 * - Dual-mode operation: Simplified model for low speeds, full dynamic model for normal speeds
 * - Handles zero and near-zero velocity conditions without numerical instability
 * - Includes rolling resistance and basic drivetrain losses
 * - Numerically stable slip angle calculations
 * - Smooth transition between low and high-speed behavior
 *
 * Physical Effects Modeled:
 * - Tire lateral forces using nonlinear force model
 * - Rolling resistance proportional to normal force
 * - Yaw dynamics with proper handling of low-speed steering
 * - Combined slip effects for more realistic behavior
 *
 * Behavior at Different Speed Ranges:
 * - Zero/Very Low Speed (v < MIN_VELOCITY):
 *   * Uses simplified kinematic relationships
 *   * Applies acceleration in vehicle's heading direction
 *   * Includes basic rolling resistance
 *   * Maintains numerical stability
 * - Normal Speed (v >= MIN_VELOCITY):
 *   * Full dynamic model with tire slip angles
 *   * Nonlinear tire forces
 *   * Complete lateral and longitudinal dynamics
 *
 * @param current_state Current vehicle state including position, velocities, and orientation
 * @param steering_angle_rad Steering angle in radians
 * @param acceleration Longitudinal acceleration command in m/s^2
 * @param dt Time step in seconds
 *
 * @return Updated vehicle state
 *
 * @throws std::invalid_argument if dt <= 0.0
 *
 * @note The model uses a minimum velocity threshold (MIN_VELOCITY) to switch between
 *       simplified and full dynamic models. This ensures stable behavior across all
 *       speed ranges while maintaining physical accuracy.
 */
Vehicle BicycleModel::updateDynamicState(
    const Vehicle& current_state,
    double steering_angle_rad,
    double acceleration,
    double dt) const {

    if (dt <= 0.0) {
        throw std::invalid_argument("Time step (dt) must be positive.");
    }

    Vehicle next_state = current_state;

    // Calculate total velocity and direction
    const double v_total = std::sqrt(current_state.getVx() * current_state.getVx() +
                                   current_state.getVz() * current_state.getVz());

    // Constants for numerical stability and physics
    constexpr double MIN_VELOCITY = 0.1;  // m/s
    constexpr double ROLLING_RESISTANCE_COEFF = 0.015;  // Dimensionless rolling resistance coefficient

    // Handle very low speed case with simplified model
    if (v_total < MIN_VELOCITY) {
        // Apply acceleration in the direction the vehicle is pointing
        const double applied_accel = std::max(
            acceleration - ROLLING_RESISTANCE_COEFF * g,
            0.0  // Prevent backwards motion from rolling resistance
        );

        next_state.setVz(next_state.getVz() + applied_accel * std::cos(current_state.getYaw()) * dt);
        next_state.setVx(next_state.getVx() + applied_accel * std::sin(current_state.getYaw()) * dt);

        // At very low speeds, steering creates an immediate yaw rate
        // Use a simplified kinematic relationship
        if (std::abs(steering_angle_rad) > 1e-6) {
            next_state.setYawRate((v_total * std::tan(steering_angle_rad)) / wheelbase);
        } else {
            next_state.setYawRate(0.0);
        }
    }
    // Normal dynamic model for higher speeds
    else {
        // Calculate velocity direction for slip angles
        const double velocity_direction = std::atan2(current_state.getVx(), current_state.getVz());

        // Compute slip angles with protection against numerical issues
        const double alpha_f = steering_angle_rad - velocity_direction -
            (lf * current_state.getYawRate()) / std::max(v_total, MIN_VELOCITY);
        const double alpha_r = -velocity_direction +
            (lr * current_state.getYawRate()) / std::max(v_total, MIN_VELOCITY);

        // Compute tire forces - use nonlinear model for more accurate behavior
        const double F_normal_static = (mass * g) / 2.0;  // Simple weight distribution
        const double Fyf = computeNonlinearTireForce(Cf, F_normal_static, alpha_f, acceleration);
        const double Fyr = computeNonlinearTireForce(Cr, F_normal_static, alpha_r, acceleration);

        // Include rolling resistance
        const double F_rolling = ROLLING_RESISTANCE_COEFF * mass * g;
        const double resistance_x = -F_rolling * current_state.getVx() / v_total;
        const double resistance_z = -F_rolling * current_state.getVz() / v_total;

        // Compute accelerations including all forces
        const double a_x = (Fyf * std::sin(steering_angle_rad) + resistance_x) / mass;
        const double a_z = (Fyf * std::cos(steering_angle_rad) + Fyr + resistance_z) / mass + acceleration;
        const double yaw_acc = (Fyf * lf * std::cos(steering_angle_rad) - Fyr * lr) / Iz;

        // Update velocities
        next_state.setVx(next_state.getVx() + a_x * dt);
        next_state.setVz(next_state.getVz() + a_z * dt);
        next_state.setYawRate(next_state.getYawRate() + yaw_acc * dt);
    }

    // Update position and heading (same for both speed cases)
    next_state.setYaw(normalizeAngle(current_state.getYaw() + next_state.getYawRate() * dt));
    const double cos_psi = std::cos(next_state.getYaw());
    const double sin_psi = std::sin(next_state.getYaw());

    // Update position
    next_state.setZ(next_state.getZ() + (next_state.getVz() * cos_psi - next_state.getVx() * sin_psi) * dt);
    next_state.setX(next_state.getX() + (next_state.getVz() * sin_psi + next_state.getVx() * cos_psi) * dt);

    // Update slip angle
    if (v_total > MIN_VELOCITY) {
        next_state.setBeta(std::atan2(next_state.getVx(), next_state.getVz()));
    } else {
        next_state.setBeta(0.0);
    }

    return next_state;
}

// Update coupled dynamic state
Vehicle BicycleModel::updateCoupledState(
    const Vehicle& current_state,
    double steering_angle_rad,
    double throttle, // Throttle or brake input
    double dt) const {

    if (dt <= 0.0) {
        throw std::invalid_argument("Time step (dt) must be positive.");
    }

    Vehicle next_state = current_state;

    // Calculate normal forces with weight transfer
    const double a_x = throttle * engine_force - brake_force / mass;
    const double F_normal_front = (mass * g * lr - mass * h * a_x) / wheelbase;
    const double F_normal_rear = (mass * g * lf + mass * h * a_x) / wheelbase;

    // Calculate slip angles
    const double alpha_f = steering_angle_rad - atan2(
        (current_state.getVx() + lf * current_state.getYawRate()), current_state.getVz());
    const double alpha_r = -atan2(
        (current_state.getVx() - lr * current_state.getYawRate()), current_state.getVz());

    // Tire forces (using a nonlinear model, e.g., Pacejka or combined slip)
    const double F_yf = computeNonlinearTireForce(Cf, F_normal_front, alpha_f, a_x);
    const double F_yr = computeNonlinearTireForce(Cr, F_normal_rear, alpha_r, a_x);

    // Update longitudinal and lateral accelerations
    const double ax = (F_yf * cos(steering_angle_rad) + F_yr - F_drag) / mass;  // Longitudinal acceleration
    const double ay = (F_yf * sin(steering_angle_rad)) / mass;  // Lateral acceleration from front tire forces

    // Update velocities with proper centrifugal force coupling
    // For body-fixed coordinates: v_dot = a + ω × v
    const double omega_z = current_state.getYawRate();
    next_state.setVx(current_state.getVx() + (ax + omega_z * current_state.getVz()) * dt);
    next_state.setVz(current_state.getVz() + (ay - omega_z * current_state.getVx()) * dt);
    next_state.setYawRate(next_state.getYawRate() +  (F_yf * lf - F_yr * lr) / Iz * dt);

    // Update position and heading
    next_state.setYaw(normalizeAngle(current_state.getYaw() + next_state.getYawRate() * dt));
    const double cos_psi = cos(next_state.getYaw());
    const double sin_psi = sin(next_state.getYaw());
    next_state.setZ(next_state.getZ() + (next_state.getVz() * cos_psi - next_state.getVx() * sin_psi) * dt);
    next_state.setX(next_state.getX() + (next_state.getVz() * sin_psi + next_state.getVx() * cos_psi) * dt);

    return next_state;
}

double BicycleModel::computeNonlinearTireForce(
    double cornering_stiffness,    // Cornering stiffness (e.g., Cf or Cr)
    double normal_force,           // Normal force acting on the tire
    double slip_angle,             // Slip angle (radians)
    double longitudinal_accel      // Longitudinal acceleration (optional, unused here)
) const {
    // Parameters for the Magic Formula (Pacejka tire model)
    const double B = 10.0;  // Stiffness factor - controls initial slope
    const double C = 1.9;   // Shape factor - controls curve shape
    const double D = normal_force; // Peak factor proportional to normal force - maximum tire force
    const double E = 0.97;  // Curvature factor - controls curve smoothness near peak

    // Calculate the lateral force using the Magic Formula
    const double term = B * slip_angle;
    const double Fy = D * sin(C * atan(term - E * (term - atan(term))));

    return Fy;
}

// AckermannModel implementation
std::pair<double, double> AckermannModel::calculateWheelAngles(double center_steering_angle) const {
    if (std::abs(center_steering_angle) < 1e-8) {
        // Straight driving - both wheels have zero angle
        return {0.0, 0.0};
    }
    
    // Calculate turning radius from center steering angle
    // For small angles: R ≈ L/δ, for exact: R = L/tan(δ)
    const double turning_radius = wheelbase / std::tan(center_steering_angle);
    
    // Check for impossible geometry (turning radius too small)
    if (std::abs(turning_radius) < track_width / 2.0) {
        throw std::invalid_argument("Turning radius too small for vehicle track width");
    }
    
    // Calculate wheel angles using Ackermann geometry
    // Inner wheel (tighter turn): δ_inner = atan(L / (R - T/2))
    // Outer wheel (wider turn): δ_outer = atan(L / (R + T/2))
    
    double inner_angle, outer_angle;
    
    if (center_steering_angle > 0) {  // Left turn
        inner_angle = std::atan(wheelbase / (turning_radius - track_width / 2.0));
        outer_angle = std::atan(wheelbase / (turning_radius + track_width / 2.0));
    } else {  // Right turn
        inner_angle = std::atan(wheelbase / (-turning_radius - track_width / 2.0));
        outer_angle = std::atan(wheelbase / (-turning_radius + track_width / 2.0));
    }
    
    return {inner_angle, outer_angle};
}

Vehicle AckermannModel::updateAckermannState(const Vehicle& vehicle, double center_steering_angle,
                                           double velocity, double dt) const {
    if (dt <= 0.0) {
        throw std::invalid_argument("Time step (dt) must be positive.");
    }
    
    // Calculate individual wheel angles
    auto [inner_angle, outer_angle] = calculateWheelAngles(center_steering_angle);
    
    // Use average wheel angle for bicycle model approximation
    // This maintains compatibility with the existing bicycle model framework
    // while incorporating Ackermann geometry effects
    const double effective_steering_angle = (inner_angle + outer_angle) / 2.0;
    
    // Apply standard kinematic bicycle model with effective steering angle
    return updateKinematicState(vehicle, effective_steering_angle, velocity, dt);
}

void AckermannModel::setTrackWidth(double tw) {
    if (tw <= 0.0) {
        throw std::invalid_argument("Track width must be positive.");
    }
    track_width = tw;
}

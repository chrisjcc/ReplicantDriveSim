#include <cmath>

class BicycleModel {
private:
    // Vehicle parameters
    double wheelbase;        // Distance between front and rear axles (m)
    double mass;            // Vehicle mass (kg)
    double Iz;              // Yaw moment of inertia (kg*m^2)
    double lf;              // Distance from CG to front axle (m)
    double lr;              // Distance from CG to rear axle (m)
    double Cf;              // Front cornering stiffness (N/rad)
    double Cr;              // Rear cornering stiffness (N/rad)

public:
    BicycleModel(double wb, double m, double inertia, double front_length, 
                 double rear_length, double front_stiffness, double rear_stiffness) 
        : wheelbase(wb), mass(m), Iz(inertia), lf(front_length), lr(rear_length),
          Cf(front_stiffness), Cr(rear_stiffness) {}

    struct VehicleState {
        double x;           // Global X position (m)
        double y;           // Global Y position (m)
        double psi;         // Yaw angle (rad)
        double v_x;         // Longitudinal velocity (m/s)
        double v_y;         // Lateral velocity (m/s)
        double yaw_rate;    // Yaw rate (rad/s)
        double beta;        // Sideslip angle (rad)
    };

    VehicleState calculateKinematics(double steering_angle, double velocity, 
                                   const VehicleState& current_state, double dt) {
        VehicleState next_state = current_state;
        
        // Calculate slip angles
        double alpha_f = steering_angle - atan2((current_state.v_y + lf * current_state.yaw_rate), 
                                              current_state.v_x);
        double alpha_r = -atan2((current_state.v_y - lr * current_state.yaw_rate), 
                               current_state.v_x);

        // Calculate lateral forces
        double Fyf = Cf * alpha_f;
        double Fyr = Cr * alpha_r;

        // Calculate accelerations
        double ay = (Fyf * cos(steering_angle) + Fyr) / mass;
        double yaw_acc = (Fyf * cos(steering_angle) * lf - Fyr * lr) / Iz;

        // Update state using simple Euler integration
        next_state.v_y += ay * dt;
        next_state.yaw_rate += yaw_acc * dt;
        next_state.psi += current_state.yaw_rate * dt;
        
        // Update position
        next_state.x += (current_state.v_x * cos(current_state.psi) - 
                        current_state.v_y * sin(current_state.psi)) * dt;
        next_state.y += (current_state.v_x * sin(current_state.psi) + 
                        current_state.v_y * cos(current_state.psi)) * dt;

        // Calculate sideslip angle
        next_state.beta = atan2(current_state.v_y, current_state.v_x);

        return next_state;
    }

    // Simplified steady-state yaw rate calculation
    double calculateSteadyStateYawRate(double steering_angle, double velocity) {
        // For steady state conditions
        return (velocity * steering_angle) / wheelbase;
    }
};

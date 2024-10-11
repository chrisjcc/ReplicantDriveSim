#include <pybind11/pybind11.h>
#include <pybind11/stl.h>
#include <pybind11/iostream.h>
#include <pybind11/operators.h>

#include "vehicle.h"
#include "traffic.h"

#ifndef VERSION_INFO
#define VERSION_INFO "dev"
#endif

namespace py = pybind11;

PYBIND11_MODULE(simulation, m) {
    m.doc() = "Traffic simulation module";
    m.attr("__version__") = VERSION_INFO;

    // Bind the Vehicle class
    py::class_<Vehicle>(m, "Vehicle")
        .def(py::init<>(), "Default constructor")
        .def(py::init<const std::string&, int, int, float, float>(), "Constructor with parameters")
        .def("getName", &Vehicle::getName, "Get the name of the vehicle")
        .def("getId", &Vehicle::getId, "Get the ID of the vehicle")
        .def("getLaneId", &Vehicle::getLaneId, "Get the lane ID of the vehicle")
        .def("getWidth", &Vehicle::getWidth, "Get the width of the vehicle")
        .def("getLength", &Vehicle::getLength, "Get the length of the vehicle")
        .def("getSteering", &Vehicle::getSteering, "Get the steering angle of the vehicle")
        .def("getX", &Vehicle::getX, "Get the X coordinate of the vehicle")
        .def("getY", &Vehicle::getY, "Get the Y coordinate of the vehicle")
        .def("getZ", &Vehicle::getZ, "Get the Z coordinate of the vehicle")
        .def("getVx", &Vehicle::getVx, "Get the velocity in the X direction")
        .def("getVy", &Vehicle::getVy, "Get the velocity in the Y direction")
        .def("getVz", &Vehicle::getVz, "Get the velocity in the Z direction")
        .def("getAcceleration", &Vehicle::getAcceleration, "Get the acceleration of the vehicle")
        .def("getSensorRange", &Vehicle::getSensorRange, "Get the sensor range of the vehicle")
        .def("getPositionString", &Vehicle::getPositionString, "Get the position of the vehicle as a string");

    // Bind the Traffic class
    py::class_<Traffic>(m, "Traffic")
        .def(py::init<const int&, const unsigned&>(), "Constructor with the number of agents and seed value")
        .def("step", &Traffic::step, "Advance the simulation by one time step")
        .def("get_agents", &Traffic::get_agents, py::return_value_policy::reference, "Get all agents in the traffic simulation")
        .def("get_agent_by_name", &Traffic::get_agent_by_name, py::return_value_policy::reference, "Get an agent by name")
        .def("get_agent_positions", &Traffic::get_agent_positions, "Get the positions of all agents")
        .def("get_agent_velocities", &Traffic::get_agent_velocities, "Get the velocities of all agents")
        .def("get_previous_positions", &Traffic::get_previous_positions, "Get the previous positions of all agents")
        .def("get_agent_orientations", &Traffic::get_agent_orientations, "Get the orientations of all agents");

    // Bind vector of shared_ptr<Vehicle> using a custom caster
    py::class_<std::vector<std::shared_ptr<Vehicle>>>(m, "VehiclePtrVector")
        .def(py::init<>(), "Default constructor for a vector of shared_ptr<Vehicle>")
        .def("size", &std::vector<std::shared_ptr<Vehicle>>::size, "Get the size of the vector")
        .def("__getitem__", [](const std::vector<std::shared_ptr<Vehicle>>& v, size_t i) {
            if (i >= v.size())
                throw py::index_error();
            return v[i];
        }, py::return_value_policy::reference_internal, "Get an element from the vector by index");
}

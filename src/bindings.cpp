#include <pybind11/pybind11.h>
#include <pybind11/stl.h>

#include "traffic_simulation.h"

namespace py = pybind11;

PYBIND11_MODULE(traffic_simulation, m) {
    m.doc() = "Traffic simulation module"; // Optional module docstring

    py::class_<Vehicle>(m, "Vehicle")
        .def(py::init<>()) // Bind the default constructor
        .def(py::init<const std::string&, int, int, float, float>()) // Bind the parameterized constructor
        .def("getName", &Vehicle::getName)
        .def("getId", &Vehicle::getId)
        .def("getLaneId", &Vehicle::getLaneId)
        .def("getWidth", &Vehicle::getWidth)
        .def("getLength", &Vehicle::getLength)
        .def("getSteering", &Vehicle::getSteering)
        .def("getX", &Vehicle::getX)
        .def("getY", &Vehicle::getY)
        .def("getZ", &Vehicle::getZ)
        .def("getVx", &Vehicle::getVx)
        .def("getVy", &Vehicle::getVy)
        .def("getVz", &Vehicle::getVz)
        .def("getAcceleration", &Vehicle::getAcceleration)
        .def("getSensorRange", &Vehicle::getSensorRange)
        .def("getPositionString", &Vehicle::getPositionString);

    py::class_<TrafficSimulation>(m, "TrafficSimulation")
        .def(py::init<int>())
        .def("step", &TrafficSimulation::step)
        .def("get_agents", &TrafficSimulation::get_agents)
        .def("get_agent_by_name", &TrafficSimulation::get_agent_by_name)
        .def("get_agent_positions", &TrafficSimulation::get_agent_positions)
        .def("get_agent_velocities", &TrafficSimulation::get_agent_velocities)
        .def("get_previous_positions", &TrafficSimulation::get_previous_positions)
        .def("get_nearby_vehicles", &TrafficSimulation::getNearbyVehicles);
}

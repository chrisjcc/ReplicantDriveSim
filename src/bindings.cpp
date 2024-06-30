#include <pybind11/pybind11.h>
#include <pybind11/stl.h>

#include "traffic_simulation.h"

namespace py = pybind11;

PYBIND11_MODULE(traffic_simulation, m) {
    m.doc() = "Traffic simulation module"; // Optional module docstring

    py::class_<Vehicle>(m, "Vehicle")
        .def("getId", &Vehicle::getId)
        .def("getName", &Vehicle::getName)
        .def("getLaneId", &Vehicle::getLaneId)
        .def("getgetWidth", &Vehicle::getWidth)
        .def("getgetLength", &Vehicle::getLength)
        .def("getX", &Vehicle::getX)
        .def("getY", &Vehicle::getY)
        .def("getZ", &Vehicle::getZ)
        .def("getVx", &Vehicle::getVx)
        .def("getVy", &Vehicle::getVy)
        .def("getVz", &Vehicle::getVz)
        .def("getSteering", &Vehicle::getSteering)
        .def("getSensorRange", &Vehicle::getSensorRange);

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

#include <pybind11/pybind11.h>
#include <pybind11/stl.h>
#include "traffic_simulation.h"

namespace py = pybind11;

PYBIND11_MODULE(traffic_simulation, m) {
    m.doc() = "Traffic simulation module"; // Optional module docstring

    py::class_<Vehicle>(m, "Vehicle")
        .def_readwrite("x", &Vehicle::x)
        .def_readwrite("y", &Vehicle::y)
        .def_readwrite("z", &Vehicle::y)
        .def_readwrite("vx", &Vehicle::vx)
        .def_readwrite("vy", &Vehicle::vy)
        .def_readwrite("vz", &Vehicle::vz)
        .def_readwrite("steering", &Vehicle::steering);

    py::class_<TrafficSimulation>(m, "TrafficSimulation")
        .def(py::init<int>())
        .def("step", &TrafficSimulation::step)
        .def("get_agent_positions", &TrafficSimulation::get_agent_positions)
        .def("get_agent_velocities", &TrafficSimulation::get_agent_velocities)
        .def("get_previous_positions", &TrafficSimulation::get_previous_positions);
}

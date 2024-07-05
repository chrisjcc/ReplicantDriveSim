#include <pybind11/pybind11.h>
#include <pybind11/stl.h>
#include <pybind11/iostream.h>
#include <pybind11/operators.h>

#include "vehicle.h"
#include "traffic.h"

namespace py = pybind11;

PYBIND11_MODULE(simulation, m) {
    m.doc() = "Traffic simulation module";

    py::class_<Vehicle>(m, "Vehicle")
        .def(py::init<>())
        .def(py::init<const std::string&, int, int, float, float>())
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

    py::class_<Traffic>(m, "Traffic")
        .def(py::init<int>())
        .def("step", &Traffic::step)
        .def("get_agents", &Traffic::get_agents, py::return_value_policy::reference)
        .def("get_agent_by_name", &Traffic::get_agent_by_name, py::return_value_policy::reference)
        .def("get_agent_positions", &Traffic::get_agent_positions)
        .def("get_agent_velocities", &Traffic::get_agent_velocities)
        .def("get_previous_positions", &Traffic::get_previous_positions)
        .def("get_nearby_vehicles", &Traffic::getNearbyVehicles);

    // Bind vector of shared_ptr<Vehicle> using a custom caster
    py::class_<std::vector<std::shared_ptr<Vehicle>>>(m, "VehiclePtrVector")
        .def(py::init<>())
        .def("size", &std::vector<std::shared_ptr<Vehicle>>::size)
        .def("__getitem__", [](const std::vector<std::shared_ptr<Vehicle>>& v, size_t i) {
            if (i >= v.size())
                throw py::index_error();
            return v[i];
        }, py::return_value_policy::reference);
}

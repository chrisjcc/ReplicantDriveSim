#include <pybind11/pybind11.h>
#include <pybind11/stl.h>
#include <pybind11/iostream.h>
#include <pybind11/operators.h>

#include "vehicle.h"
#include "traffic.h"

#include "OpenDriveMap.h"         // Ensure you include the header for OpenDriveMap
#include "Road.h"                 // Include the header for Road class
#include "RoadNetworkMesh.h"      // Include the header for RoadNetworkMesh

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

    // Bind odr::Road with a default constructor
    py::class_<odr::Road>(m, "Road") // Bind the default constructor and methods
        .def(py::init<std::string, double, std::string, std::string, bool>(),  // Constructor with specific arguments
             py::arg("id"),
             py::arg("length"),
             py::arg("junction"),
             py::arg("name"),
             py::arg("left_hand_traffic") = false)  // Default argument for bool;
        .def_readwrite("id", &odr::Road::id)
        .def_readwrite("length", &odr::Road::length)
        .def_readwrite("junction", &odr::Road::junction)
        .def_readwrite("name", &odr::Road::name)
        .def_readwrite("left_hand_traffic", &odr::Road::left_hand_traffic)
        .def("get_xyz", &odr::Road::get_xyz);

    // Bind odr::RoadNetworkMesh with a default constructor (adjust as per actual constructors)
    py::class_<odr::RoadNetworkMesh>(m, "RoadNetworkMesh")
        .def(py::init<>());  // Add appropriate constructor and methods

    // Bind odr::OpenDriveMap with constructors and methods
    py::class_<odr::OpenDriveMap, std::shared_ptr<odr::OpenDriveMap>>(m, "OpenDriveMap")
        .def(py::init<const std::string&>())
        .def("get_road_network_mesh", &odr::OpenDriveMap::get_road_network_mesh)  // Expose the method
        .def("get_roads", &odr::OpenDriveMap::get_roads);  // Expose and bind the get_roads method


    py::class_<Traffic>(m, "Traffic")
        .def(py::init<int, const std::string&>())
        .def("step", &Traffic::step)
        .def("get_agents", &Traffic::get_agents, py::return_value_policy::reference)
        .def("get_agent_by_name", &Traffic::get_agent_by_name, py::return_value_policy::reference)
        .def("get_agent_positions", &Traffic::get_agent_positions)
        .def("get_agent_velocities", &Traffic::get_agent_velocities)
        .def("get_previous_positions", &Traffic::get_previous_positions)
        .def("get_nearby_vehicles", &Traffic::getNearbyVehicles)
        .def_property("odr_map",
                      [](const Traffic& ts) { return ts.get_odr_map(); },
                      [](Traffic& ts, const std::shared_ptr<odr::OpenDriveMap>& map) { ts.set_odr_map(map); });

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

#include <pybind11/pybind11.h>
#include <pybind11/stl.h>

#include "traffic_simulation.h"   // Adjust this include path as per your project structure
#include "OpenDriveMap.h"         // Ensure you include the header for OpenDriveMap
#include "Road.h"                 // Include the header for Road class
#include "RoadNetworkMesh.h"      // Include the header for RoadNetworkMesh

namespace py = pybind11;

PYBIND11_MODULE(traffic_simulation, m) {
    m.doc() = "Traffic simulation module"; // Optional module docstring

    py::class_<Vehicle>(m, "Vehicle")
        .def("getId", &Vehicle::getId)
        .def("getLaneId", &Vehicle::getLaneId)
        .def("getgetWidth", &Vehicle::getWidth)
        .def("getgetLength", &Vehicle::getLength)
        .def("getX", &Vehicle::getX)
        .def("getY", &Vehicle::getY)
        .def("getZ", &Vehicle::getZ)
        .def("getVx", &Vehicle::getVx)
        .def("getVy", &Vehicle::getVy)
        .def("getVz", &Vehicle::getVz)
        .def("getSteering", &Vehicle::getSteering);

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

    // Bind TrafficSimulation class with methods and properties
    py::class_<TrafficSimulation>(m, "TrafficSimulation")
        .def(py::init<int, const std::string&, float>())
        .def("get_agents", &TrafficSimulation::get_agents)
        .def("step", &TrafficSimulation::step)
        .def("get_agent_positions", &TrafficSimulation::get_agent_positions)
        .def("get_agent_velocities", &TrafficSimulation::get_agent_velocities)
        .def("get_previous_positions", &TrafficSimulation::get_previous_positions)
        .def_property("odr_map",
                      [](const TrafficSimulation& ts) { return ts.get_odr_map(); },
                      [](TrafficSimulation& ts, const std::shared_ptr<odr::OpenDriveMap>& map) { ts.set_odr_map(map); });
}

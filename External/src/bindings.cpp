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

    // Bind the Traffic class
    py::class_<Traffic>(m, "Traffic")
        .def(py::init<const int&, const std::string&, const unsigned&>(), "Constructor with the number of agents and seed value")
        .def("step", &Traffic::step, "Advance the simulation by one time step")
        .def("get_agents", &Traffic::getAgents, py::return_value_policy::reference, "Get all agents in the traffic simulation")
        .def("get_agent_by_name", &Traffic::getAgentByName, py::return_value_policy::reference, "Get an agent by name")
        .def("get_agent_positions", &Traffic::getAgentPositions, "Get the positions of all agents")
        .def("get_agent_velocities", &Traffic::getAgentVelocities, "Get the velocities of all agents")
        .def("get_previous_positions", &Traffic::getPreviousPositions, "Get the previous positions of all agents")
        .def("get_agent_orientations", &Traffic::getAgentOrientations, "Get the orientations of all agents")
        .def("get_nearby_vehicles", &Traffic::getNearbyVehicles, py::return_value_policy::reference_internal, "Get nearby vehicles for each agent")
        .def_property("odr_map",
                      [](const Traffic& ts) { return ts.getOdrMap(); },
                      [](Traffic& ts, const std::shared_ptr<odr::OpenDriveMap>& map) { ts.setOdrMap(map); });

    // Bind odr::RoadNetworkMesh with a default constructor (adjust as per actual constructors)
    py::class_<odr::RoadNetworkMesh>(m, "RoadNetworkMesh")
        .def(py::init<>());  // Add appropriate constructor and methods

    // Bind odr::OpenDriveMap with constructors and methods
    py::class_<odr::OpenDriveMap, std::shared_ptr<odr::OpenDriveMap>>(m, "OpenDriveMap")
        .def(py::init<const std::string&>())
        .def("get_road_network_mesh", &odr::OpenDriveMap::get_road_network_mesh)  // Expose the method
        .def("get_roads", &odr::OpenDriveMap::get_roads);  // Expose and bind the get_roads method

    // Bind vector of shared_ptr<Vehicle> using a custom class
    py::class_<std::vector<std::shared_ptr<Vehicle>>>(m, "VehiclePtrVector")
        .def(py::init<>(), "Default constructor for a vector of shared_ptr<Vehicle>")
        .def("size", &std::vector<std::shared_ptr<Vehicle>>::size, "Get the size of the vector")
        .def("__getitem__", [](const std::vector<std::shared_ptr<Vehicle>>& v, size_t i) {
            if (i >= v.size())
                throw py::index_error();
            return v[i];
        }, py::return_value_policy::reference_internal, "Get an element from the vector by index");
}

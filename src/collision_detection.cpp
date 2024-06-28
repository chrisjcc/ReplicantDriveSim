#include "collision_detection.h"

/**
 * @brief Constructs a SpatialHash with the given cell size.
 * @param cell_size Size of each cell in the spatial hash grid.
 */
SpatialHash::SpatialHash(float cell_size)
    : cell_size(cell_size) {}

/**
 * @brief Inserts a vehicle into the spatial hash grid.
 * @param vehicle Pointer to the vehicle to be inserted.
 */
void SpatialHash::insert(Vehicle* vehicle) {
    int cell_x = static_cast<int>(std::floor(vehicle->getX() / cell_size));
    int cell_y = static_cast<int>(std::floor(vehicle->getY() / cell_size));
    cells[{cell_x, cell_y}].push_back(vehicle);
}

/**
 * @brief Gets potential collisions for a given vehicle.
 * @param vehicle Pointer to the vehicle to check for potential collisions.
 * @return A vector of pointers to vehicles that may collide with the given vehicle.
 */
std::vector<Vehicle*> SpatialHash::getPotentialCollisions(Vehicle* vehicle) {
    int cell_x = static_cast<int>(std::floor(vehicle->getX() / cell_size));
    int cell_y = static_cast<int>(std::floor(vehicle->getY() / cell_size));
    
    std::vector<Vehicle*> potential_collisions;
    for (int dx = -1; dx <= 1; ++dx) {
        for (int dy = -1; dy <= 1; ++dy) {
            auto it = cells.find({cell_x + dx, cell_y + dy});
            if (it != cells.end()) {
                potential_collisions.insert(potential_collisions.end(), it->second.begin(), it->second.end());
            }
        }
    }
    return potential_collisions;
}

/**
 * @brief Clears all vehicles from the spatial hash grid.
 */
void SpatialHash::clear() {
    cells.clear();
}

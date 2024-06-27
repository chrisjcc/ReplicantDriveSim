#ifndef COLLISION_DETECTION_H
#define COLLISION_DETECTION_H

#include <unordered_map>
#include <vector>
#include <cmath>
#include <functional> // Added for std::hash

#include "vehicle.h"

/**
 * @class SpatialHash
 * @brief A spatial hash grid for efficient collision detection.
 */
class SpatialHash {
public:
    /**
     * @brief Constructs a SpatialHash with the given cell size.
     * @param cell_size Size of each cell in the spatial hash grid.
     */
    SpatialHash(float cell_size);

    /**
     * @brief Inserts a vehicle into the spatial hash grid.
     * @param vehicle Pointer to the vehicle to be inserted.
     */
    void insert(Vehicle* vehicle);

    /**
     * @brief Gets potential collisions for a given vehicle.
     * @param vehicle Pointer to the vehicle to check for potential collisions.
     * @return A vector of pointers to vehicles that may collide with the given vehicle.
     */
    std::vector<Vehicle*> getPotentialCollisions(Vehicle* vehicle);

    /**
     * @brief Clears all vehicles from the spatial hash grid.
     */
    void clear();

private:
    float cell_size; ///< Size of each cell in the spatial hash grid.

    /**
     * @struct pair_hash
     * @brief A hash function for pairs of integers.
     */
    struct pair_hash {
        template <class T1, class T2>
        std::size_t operator()(const std::pair<T1, T2>& p) const {
            auto hash1 = std::hash<T1>{}(p.first);
            auto hash2 = std::hash<T2>{}(p.second);
            return hash1 ^ hash2;
        }
    };

    std::unordered_map<std::pair<int, int>, std::vector<Vehicle*>, pair_hash> cells; ///< Map from cell coordinates to vehicles.
};

#endif // COLLISION_DETECTION_H

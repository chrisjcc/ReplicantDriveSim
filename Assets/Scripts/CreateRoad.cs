using UnityEngine;

/// <summary>
/// Creates a road surface and boundaries in the Unity scene.
///
/// This script is responsible for:
/// 1. Generating a textured road surface with appropriate physics properties.
/// 2. Creating invisible boundary walls on both sides of the road.
///
/// Key Components:
/// - Road Surface: A scaled plane with custom texture and physics material.
/// - Road Boundaries: Invisible walls to prevent agents from leaving the road.
///
/// Public Properties:
/// - roadTexture: Texture2D for the road surface.
/// - roadPhysicsMaterial: PhysicMaterial for the road's friction properties.
/// - roadWidth: Width of the road (default 30 units).
/// - roadLength: Length of the road (default 3250 units).
/// - boundaryHeight: Height of the boundary walls (default 5 units).
///
/// Key Methods:
/// - Start(): Initializes road creation on script start.
/// - CreateRoadSurface(): Generates the main road surface.
/// - CreateRoadBoundaries(): Creates boundary walls on both sides of the road.
/// - CreateBoundary(): Helper method to create a single boundary wall.
///
/// Road Properties:
/// - Uses a plane primitive scaled to road dimensions.
/// - Custom material with tiled texture.
/// - MeshCollider with custom physics material for realistic vehicle interactions.
/// - Positioned slightly above y=0 (0.15 units) to prevent z-fighting.
/// - Assigned to "Road" layer (must be created in Unity's layer settings).
///
/// Boundary Properties:
/// - Invisible cube primitives scaled to road length.
/// - Tagged as "RoadBoundary" for easy identification.
/// - Assigned to "TrafficAgent" layer (adjust as needed).
///
/// Usage:
/// Attach this script to an empty GameObject in the scene. It will automatically
/// create the road and boundaries when the scene starts.
///
/// Note:
/// - Ensure required layers ("Road", "TrafficAgent") are created in Unity's Tags and Layers settings.
/// - Adjust public properties in the inspector to customize road dimensions and appearance.
/// - Consider adding options for curved roads or intersections for more complex scenarios.
/// - The script currently creates a straight road; modify for different road layouts if needed.
///
/// </summary>
public class CreateRoad : MonoBehaviour
{
    public Texture2D roadTexture;
    public PhysicMaterial roadPhysicsMaterial;
    public float roadWidth = 30f; // 3 * 10 units wide
    public float roadLength = 3250f; // 325 * 10 units long
    public float boundaryHeight = 5f;

    /// <summary>
    /// Initializes the road creation process when the script starts.
    ///
    /// This method is automatically called by Unity when the script instance is being loaded.
    /// It sequentially executes two main operations:
    /// 1. Creates the road surface by calling CreateRoadSurface().
    /// 2. Creates the road boundaries by calling CreateRoadBoundaries().
    ///
    /// Usage:
    /// This method doesn't need to be called manually. Unity will invoke it
    /// when the GameObject this script is attached to becomes active in the scene.
    ///
    /// Note:
    /// - Ensure that all necessary public properties (like roadTexture, roadPhysicsMaterial)
    ///   are set in the Unity Inspector before the scene starts.
    /// - If you need to recreate the road at runtime, consider creating public methods
    ///   that call CreateRoadSurface() and CreateRoadBoundaries() individually.
    ///
    /// </summary>
    void Start()
    {
        CreateRoadSurface();
        CreateRoadBoundaries();
    }

    /// <summary>
    /// Creates the road surface in the Unity scene.
    ///
    /// This method performs the following operations:
    /// 1. Initializes or validates the road's physics material.
    /// 2. Creates a plane primitive to represent the road surface.
    /// 3. Scales the plane to match specified road dimensions.
    /// 4. Applies a texture to the road surface with appropriate tiling.
    /// 5. Positions the road in the scene.
    /// 6. Sets up a MeshCollider for physics interactions.
    /// 7. Assigns the road to a specific layer for collision management.
    ///
    /// Key Components:
    /// - Physics Material: Defines friction properties (high dynamic friction, no static friction).
    /// - Road GameObject: A scaled plane primitive named "Road".
    /// - Road Material: Uses the Standard shader with a custom texture.
    /// - Collider: MeshCollider with the custom physics material applied.
    ///
    /// Texture Handling:
    /// - The texture is tiled along the length of the road.
    /// - Tiling is adjusted based on the road's length.
    ///
    /// Positioning:
    /// - The road is positioned slightly above y=0 (0.15 units) to prevent z-fighting.
    /// - Centered at x=0, with z position at half the road length.
    ///
    /// Layer Assignment:
    /// - Attempts to assign the road to a "Road" layer.
    /// - Logs a warning if the "Road" layer doesn't exist.
    ///
    /// Dependencies:
    /// - Requires roadTexture to be set (public field).
    /// - Uses roadWidth and roadLength for dimensions (public fields).
    ///
    /// Note:
    /// - Ensure the "Road" layer is created in Unity's Tags and Layers settings.
    /// - The road's scale is adjusted by dividing width and length by 10 to account for Unity's plane size.
    /// - Consider adding error checking for missing roadTexture.
    /// - The method assumes a straight, flat road. Modify for more complex road shapes if needed.
    ///
    /// </summary>
    void CreateRoadSurface()
    {
        // Ensure roadPhysicsMaterial is not null and initialize if needed
        if (roadPhysicsMaterial == null)
        {
            roadPhysicsMaterial = new PhysicMaterial("RoadMaterial");
            roadPhysicsMaterial.dynamicFriction = 1f; // High friction
            roadPhysicsMaterial.staticFriction = 0f; // No bounce
        }

        // Create a new GameObject
        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
        road.name = "Road";

        // Scale the Plane to make it a long road
        road.transform.localScale = new Vector3(roadWidth / 10f, 10, roadLength / 10f);

        // Create a new material and apply the road texture
        Material roadMaterial = new Material(Shader.Find("Standard"));
        roadMaterial.mainTexture = roadTexture;
        road.GetComponent<Renderer>().material = roadMaterial;

        // Adjust the texture tiling
        roadMaterial.mainTextureScale = new Vector2(1, roadLength / 10f);

        // Position the road
        road.transform.position = new Vector3(0, 0.15f, roadLength / 2f);

        // Add or get the existing MeshCollider
        MeshCollider roadCollider = road.GetComponent<MeshCollider>();
        if (roadCollider == null)
        {
            roadCollider = road.AddComponent<MeshCollider>();
        }

        // Ensure the collider is not a trigger
        roadCollider.isTrigger = false;

        // Apply the physics material
        roadCollider.material = roadPhysicsMaterial;

        // Set the layer (assuming you've created a "Road" layer)
        int roadLayer = LayerMask.NameToLayer("Road");
        if (roadLayer != -1)  // Check if the layer exists
        {
            road.layer = roadLayer;
        }
        else
        {
            Debug.LogWarning("Layer 'Road' does not exist. Please add it in the Tags and Layers settings.");
        }
    }

    /// <summary>
    /// Creates the boundaries on both sides of the road.
    ///
    /// This method is responsible for:
    /// 1. Creating a right boundary at half the road width to the right of the center.
    /// 2. Creating a left boundary at half the road width to the left of the center.
    ///
    /// Operation:
    /// - Calls the CreateBoundary method twice, once for each side of the road.
    /// - Uses the roadWidth property to determine the position of each boundary.
    ///
    /// Parameters used:
    /// - roadWidth: The total width of the road, used to calculate boundary positions.
    ///
    /// Boundary Naming:
    /// - Right boundary is named "RightBoundary".
    /// - Left boundary is named "LeftBoundary".
    ///
    /// Positioning:
    /// - Right boundary is placed at +roadWidth/2 on the x-axis.
    /// - Left boundary is placed at -roadWidth/2 on the x-axis.
    ///
    /// Usage:
    /// This method is typically called from the Start method to set up road boundaries
    /// immediately after the road surface is created.
    ///
    /// Note:
    /// - Ensure that the CreateBoundary method is properly implemented to handle the actual
    ///   creation of each boundary object.
    /// - The boundaries are symmetrically placed on either side of the road center.
    /// - Consider adding parameters for custom boundary placement if more complex road
    ///   layouts are needed in the future.
    ///
    /// </summary>
    void CreateRoadBoundaries()
    {
        CreateBoundary(roadWidth / 2f, "RightBoundary");
        CreateBoundary(-roadWidth / 2f, "LeftBoundary");
    }

    /// <summary>
    /// Creates a single road boundary object in the Unity scene.
    ///
    /// This method is responsible for:
    /// 1. Creating a cube primitive to represent the boundary.
    /// 2. Scaling and positioning the boundary based on road dimensions.
    /// 3. Setting visibility properties of the boundary.
    /// 4. Ensuring the boundary has a collider for physics interactions.
    /// 5. Tagging and assigning a layer to the boundary for identification and collision management.
    ///
    /// Parameters:
    /// - xPosition: Float value representing the x-coordinate where the boundary should be placed.
    /// - name: String to be used as the name of the boundary GameObject.
    ///
    /// Boundary Properties:
    /// - Shape: Cube primitive
    /// - Scale: 1 unit wide, height set by boundaryHeight, length matches roadLength
    /// - Position:
    ///   * X: Determined by xPosition parameter
    ///   * Y: Half of boundaryHeight (centers the boundary vertically)
    ///   * Z: Half of roadLength (centers the boundary along the road's length)
    /// - Visibility: Renderer is enabled (Note: Comment suggests it should be invisible, but code sets it to visible)
    /// - Collider: Ensures a BoxCollider is present
    /// - Tag: Set to "RoadBoundary"
    /// - Layer: Attempts to set to "TrafficAgent" layer
    ///
    /// Error Handling:
    /// - Logs a warning if the "TrafficAgent" layer doesn't exist.
    ///
    /// Dependencies:
    /// - Uses boundaryHeight and roadLength properties from the parent class.
    ///
    /// Note:
    /// - The boundary is currently set to visible (boundaryRenderer.enabled = true), which contradicts the comment.
    ///   Consider changing to false if invisibility is desired.
    /// - The method uses "TrafficAgent" layer instead of a dedicated boundary layer. Consider creating a specific
    ///   "RoadBoundary" layer for better organization.
    /// - Ensure that the "RoadBoundary" tag is defined in Unity's Tags and Layers settings.
    /// - Consider adding parameters for customizing the boundary's width and material if needed for future enhancements.
    ///
    /// </summary>
    /// <param name="xPosition">The x-coordinate where the boundary should be placed.</param>
    /// <param name="name">The name to be assigned to the boundary GameObject.</param>
    void CreateBoundary(float xPosition, string name)
    {
        GameObject boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boundary.name = name;

        // Scale the boundary
        boundary.transform.localScale = new Vector3(1f, boundaryHeight, roadLength);

        // Position the boundary
        boundary.transform.position = new Vector3(xPosition, boundaryHeight / 2f, roadLength / 2f);

        // Make the boundary invisible
        Renderer boundaryRenderer = boundary.GetComponent<Renderer>();
        boundaryRenderer.enabled = true; // Default: false

        // Ensure it has a collider
        BoxCollider boundaryCollider = boundary.GetComponent<BoxCollider>();
        if (boundaryCollider == null)
        {
            boundaryCollider = boundary.AddComponent<BoxCollider>();
        }

        // Tag the boundary
        boundary.tag = "RoadBoundary";

        // Set the layer (you might want to create a specific layer for boundaries)
        int boundaryLayer = LayerMask.NameToLayer("TrafficAgent");
        if (boundaryLayer != -1)
        {
            boundary.layer = boundaryLayer;
        }
        else
        {
            Debug.LogWarning("Layer 'RoadBoundary' does not exist. Using default layer.");
        }
    }
}
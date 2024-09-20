using UnityEngine;

public class CreateRoad : MonoBehaviour
{
    public Texture2D roadTexture;
    public PhysicMaterial roadPhysicsMaterial;
    public float roadWidth = 30f; // 3 * 10 units wide
    public float roadLength = 3250f; // 325 * 10 units long
    public float boundaryHeight = 5f;

    void Start()
    {
        CreateRoadSurface();
        CreateRoadBoundaries();
    }

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

    void CreateRoadBoundaries()
    {
        CreateBoundary(roadWidth / 2f, "RightBoundary");
        CreateBoundary(-roadWidth / 2f, "LeftBoundary");
    }

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
        int boundaryLayer = LayerMask.NameToLayer("RoadBoundary");
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


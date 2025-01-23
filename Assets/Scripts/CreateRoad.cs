using UnityEngine;

/// <summary>
/// Creates two road surfaces (one for each direction) and boundaries in the Unity scene.
///
/// This script is responsible for:
/// 1. Generating two textured road surfaces with appropriate physics properties.
/// 2. Creating invisible boundary walls on both sides of each road and between the roads.
///
/// Key Components:
/// - Road Surfaces: Two scaled planes with custom texture and physics material.
/// - Road Boundaries: Invisible walls to prevent agents from leaving the roads or crossing to the opposite road.
///
/// Public Properties:
/// - roadTexture: Texture2D for the road surfaces.
/// - roadPhysicsMaterial: PhysicMaterial for the roads' friction properties.
/// - singleRoadWidth: Width of each road (default 15 units, accommodating two lanes).
/// - roadLength: Length of the roads (default 3250 units).
/// - boundaryHeight: Height of the boundary walls (default 5 units).
/// - medianWidth: Width of the median between the two roads.
///
/// Usage:
/// Attach this script to an empty GameObject in the scene. It will automatically
/// create the roads and boundaries when the scene starts.
///
/// Note:
/// - Ensure required layers ("Road", "TrafficAgent", "RoadBoundary") are created in Unity's Tags and Layers settings.
/// - Adjust public properties in the inspector to customize road dimensions and appearance.
/// </summary>
public class CreateDualRoad : MonoBehaviour
{
    public Texture2D roadTexture;
    public PhysicsMaterial roadPhysicsMaterial;

    public float singleRoadWidth = 15f; // Width for a single road (2 lanes)
    public float roadLength = 3250f;
    public float boundaryHeight = 5f;
    public float medianWidth = 5f; // Width of the median between the two roads

    private int roadLayer;
    private int roadBoundaryLayer;

    void Start()
    {
        // Ensure layers are set correctly
        roadLayer = LayerMask.NameToLayer("Road");
        roadBoundaryLayer = LayerMask.NameToLayer("RoadBoundary");

        if (roadLayer == -1 || roadBoundaryLayer == -1)
        {
            Debug.LogError("Required layers 'Road' or 'RoadBoundary' are missing. Please add them in Project Settings.");
            return;
        }

        CreateRoadSurfaces();
        CreateRoadBoundaries();
    }

    void CreateRoadSurfaces()
    {
        // Ensure roadPhysicsMaterial is not null and initialize if needed
        if (roadPhysicsMaterial == null)
        {
            roadPhysicsMaterial = new PhysicsMaterial("RoadMaterial");
            roadPhysicsMaterial.dynamicFriction = 1f;
            roadPhysicsMaterial.staticFriction = 0f;
        }

        CreateRoadSurface("RoadNorthbound", singleRoadWidth / 2f + medianWidth / 2f);
        CreateRoadSurface("RoadSouthbound", -singleRoadWidth / 2f - medianWidth / 2f);
    }

    void CreateRoadSurface(string name, float xPosition)
    {
        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Plane);
        road.name = name;
        road.layer = roadLayer;

        road.transform.localScale = new Vector3(singleRoadWidth / 10f, 10, roadLength / 10f);

        Material roadMaterial = new Material(Shader.Find("Standard"));
        roadMaterial.mainTexture = roadTexture;
        road.GetComponent<Renderer>().material = roadMaterial;

        roadMaterial.mainTextureScale = new Vector2(1, roadLength / 10f);

        road.transform.position = new Vector3(xPosition, 0.15f, roadLength / 2f);

        MeshCollider roadCollider = road.GetComponent<MeshCollider>();
        if (roadCollider == null)
        {
            roadCollider = road.AddComponent<MeshCollider>();
        }
        roadCollider.convex = false;  // For static terrain
        roadCollider.isTrigger = false;
        roadCollider.material = roadPhysicsMaterial;

        // Set the parent to the GameObject this script is attached to
        road.transform.SetParent(this.transform, false);
    }

    void CreateRoadBoundaries()
    {
        float totalWidth = singleRoadWidth * 2 + medianWidth;

        // Outer boundaries
        CreateBoundary(totalWidth / 2f, "RightBoundary");
        CreateBoundary(-totalWidth / 2f, "LeftBoundary");

        // Median (center) boundary
        CreateBoundary(0f, "MedianBoundary");
    }

    void CreateBoundary(float xPosition, string name)
    {
        GameObject boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
        boundary.name = name;
        boundary.layer = roadBoundaryLayer; // Assign to the RoadBoundary layer

        // For the median, use the medianWidth. For outer boundaries, use a thin width.
        float width = (name == "MedianBoundary") ? medianWidth : 1f;

        boundary.transform.localScale = new Vector3(width, boundaryHeight, roadLength);

        boundary.transform.position = new Vector3(xPosition, boundaryHeight / 2f, roadLength / 2f);
        boundary.tag = "RoadBoundary";

        Renderer boundaryRenderer = boundary.GetComponent<Renderer>();
        boundaryRenderer.enabled = false; // Make the boundary invisible

        BoxCollider boundaryCollider = boundary.GetComponent<BoxCollider>();
        if (boundaryCollider == null)
        {
            boundaryCollider = boundary.AddComponent<BoxCollider>();
        }

        boundaryCollider.isTrigger = false;

        // Mark the boundary as static for performance
        boundary.isStatic = true;

        // Set the parent to the GameObject this script is attached to
        boundary.transform.SetParent(this.transform, false);
    }
}

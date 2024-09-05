using UnityEngine;

public class CreateRoad : MonoBehaviour
{
    public Texture2D roadTexture;
    public PhysicMaterial roadPhysicsMaterial;

    void Start()
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
        road.transform.localScale = new Vector3(3, 10, 300);

        // Create a new material and apply the road texture
        Material roadMaterial = new Material(Shader.Find("Standard"));
        roadMaterial.mainTexture = roadTexture;
        road.GetComponent<Renderer>().material = roadMaterial;

        // Adjust the texture tiling
        roadMaterial.mainTextureScale = new Vector2(1, 10);

        // Position the road
        road.transform.position = new Vector3(0, 0.15f, 0);

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
}


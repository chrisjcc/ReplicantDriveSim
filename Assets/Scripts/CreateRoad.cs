using UnityEngine;

public class CreateRoad : MonoBehaviour
{
    public Texture2D roadTexture;

    void Start()
    {
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

        // Optional: position the road if necessary
        road.transform.position = new Vector3(0, (float)0.15, 0);
    }
}


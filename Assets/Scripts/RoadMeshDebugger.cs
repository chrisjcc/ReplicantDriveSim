using UnityEngine;

// Debug script to help visualize and fix road mesh rendering issues
public class RoadMeshDebugger : MonoBehaviour
{
    [Header("Debug Controls")]
    public bool showDebugInfo = true;
    public bool regenerateMesh = false;
    public float roadHeight = 0.1f;
    public bool forceUpdateMaterial = false;

    [Header("Material Override")]
    public Material debugMaterial;

    void Update()
    {
        if (regenerateMesh)
        {
            regenerateMesh = false;
            RegenerateRoadMesh();
        }

        if (forceUpdateMaterial)
        {
            forceUpdateMaterial = false;
            UpdateRoadMaterial();
        }
    }

    [ContextMenu("Regenerate Road Mesh")]
    public void RegenerateRoadMesh()
    {
        Debug.Log("RoadMeshDebugger: Regenerating road mesh");

        // Find MapAccessorRendererSafe and reload
        MapAccessorRendererSafe mapRenderer = FindFirstObjectByType<MapAccessorRendererSafe>();
        if (mapRenderer != null)
        {
            mapRenderer.ReloadMap();
            Debug.Log("RoadMeshDebugger: Map reloaded");
        }
        else
        {
            Debug.LogError("RoadMeshDebugger: MapAccessorRendererSafe not found");
        }
    }

    [ContextMenu("Update Road Material")]
    public void UpdateRoadMaterial()
    {
        GameObject roadObj = GameObject.Find("OpenDriveRoad");
        if (roadObj != null)
        {
            MeshRenderer renderer = roadObj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                if (debugMaterial != null)
                {
                    renderer.material = debugMaterial;
                    Debug.Log($"RoadMeshDebugger: Applied debug material: {debugMaterial.name}");
                }
                else
                {
                    // Create bright test material
                    Material testMaterial = new Material(Shader.Find("Standard"));
                    testMaterial.color = Color.yellow;
                    testMaterial.SetFloat("_Metallic", 0f);
                    testMaterial.SetFloat("_Smoothness", 0.5f);
                    renderer.material = testMaterial;
                    Debug.Log("RoadMeshDebugger: Applied bright yellow test material");
                }
            }
        }
    }

    [ContextMenu("Raise Road Height")]
    public void RaiseRoadHeight()
    {
        GameObject roadObj = GameObject.Find("OpenDriveRoad");
        if (roadObj != null)
        {
            Transform roadTransform = roadObj.transform;
            Vector3 newPos = roadTransform.position;
            newPos.y = roadHeight;
            roadTransform.position = newPos;
            Debug.Log($"RoadMeshDebugger: Raised road to height {roadHeight}");
        }
    }

    [ContextMenu("Log Mesh Info")]
    public void LogMeshInfo()
    {
        GameObject roadObj = GameObject.Find("OpenDriveRoad");
        if (roadObj != null)
        {
            MeshFilter meshFilter = roadObj.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = roadObj.GetComponent<MeshRenderer>();

            if (meshFilter != null && meshFilter.mesh != null)
            {
                Mesh mesh = meshFilter.mesh;
                Debug.Log($"=== ROAD MESH INFO ===");
                Debug.Log($"Vertices: {mesh.vertexCount}");
                Debug.Log($"Triangles: {mesh.triangles.Length / 3}");
                Debug.Log($"Bounds: {mesh.bounds}");
                Debug.Log($"Name: {mesh.name}");

                if (meshRenderer != null)
                {
                    Debug.Log($"Material: {meshRenderer.material?.name ?? "None"}");
                    Debug.Log($"Enabled: {meshRenderer.enabled}");
                    Debug.Log($"Shadow casting: {meshRenderer.shadowCastingMode}");
                }

                // Check first few vertices
                if (mesh.vertexCount > 0)
                {
                    Debug.Log($"First vertex: {mesh.vertices[0]}");
                    if (mesh.vertexCount > 1)
                    {
                        Debug.Log($"Second vertex: {mesh.vertices[1]}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("RoadMeshDebugger: No mesh found");
            }
        }
        else
        {
            Debug.LogWarning("RoadMeshDebugger: OpenDriveRoad GameObject not found");
        }
    }

    [ContextMenu("Create Test Road Quad")]
    public void CreateTestRoadQuad()
    {
        // Create a simple visible test quad to verify rendering
        GameObject testRoad = GameObject.Find("TestRoad");
        if (testRoad != null)
        {
            DestroyImmediate(testRoad);
        }

        testRoad = new GameObject("TestRoad");
        MeshFilter meshFilter = testRoad.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = testRoad.AddComponent<MeshRenderer>();

        // Create simple quad mesh
        Mesh quadMesh = new Mesh();
        quadMesh.vertices = new Vector3[]
        {
            new Vector3(-10, 0.2f, -10),
            new Vector3(10, 0.2f, -10),
            new Vector3(-10, 0.2f, 10),
            new Vector3(10, 0.2f, 10)
        };
        quadMesh.triangles = new int[] { 0, 2, 1, 1, 2, 3 };
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();

        meshFilter.mesh = quadMesh;

        // Bright test material
        Material testMaterial = new Material(Shader.Find("Standard"));
        testMaterial.color = Color.red;
        meshRenderer.material = testMaterial;

        Debug.Log("RoadMeshDebugger: Created bright red test quad");
    }
}
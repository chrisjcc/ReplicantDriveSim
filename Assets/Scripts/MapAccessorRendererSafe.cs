using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
#endif

// P/Invoke-free MapAccessorRenderer that uses UnityPluginBridge
public class MapAccessorRendererSafe : MonoBehaviour
{
    [Header("OpenDRIVE Map Settings")]
    [SerializeField]
    public int selectedMapIndex = 0;

    [SerializeField]
    public string[] availableMaps;

    public string mapFilePath = "Assets/Maps/data.xodr";

    [Header("Rendering Settings")]
    public Material roadMaterial;
    public string meshName = "OpenDriveRoadMesh";
    public float meshResolution = 0.5f; // Distance between mesh points in meters

    private IntPtr mapAccessor = IntPtr.Zero;
    private UnityPluginBridge pluginBridge;
    private bool isMapLoaded = false;

    void Start()
    {
        Debug.Log("MapAccessorRendererSafe: Starting P/Invoke-free OpenDRIVE rendering");

        // Start coroutine to wait for UnityPluginBridge to be available
        StartCoroutine(WaitForPluginBridge());
    }

    private IEnumerator WaitForPluginBridge()
    {
        // Wait for SceneBootstrapper to create the UnityPluginBridge
        float timeout = 10f; // 10 second timeout
        float elapsed = 0f;

        while (pluginBridge == null && elapsed < timeout)
        {
            pluginBridge = FindFirstObjectByType<UnityPluginBridge>();
            if (pluginBridge != null)
            {
                Debug.Log("MapAccessorRendererSafe: Found UnityPluginBridge, proceeding with map loading");
                break;
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (pluginBridge == null)
        {
            Debug.LogError("MapAccessorRendererSafe: UnityPluginBridge not found after timeout! Creating fallback bridge.");
            GameObject bridgeObj = new GameObject("UnityPluginBridge");
            pluginBridge = bridgeObj.AddComponent<UnityPluginBridge>();
        }

        // Now try to load the map
        LoadOpenDriveMap();
    }

    public void LoadOpenDriveMap()
    {
        if (isMapLoaded)
        {
            Debug.Log("MapAccessorRendererSafe: Map already loaded, skipping");
            return;
        }

        Debug.Log($"MapAccessorRendererSafe: Loading OpenDRIVE map from: {mapFilePath}");

        try
        {
            // Create map accessor using the bridge
            mapAccessor = pluginBridge.CreateMapAccessor(mapFilePath);

            if (mapAccessor == IntPtr.Zero)
            {
                Debug.LogError("MapAccessorRendererSafe: Failed to create map accessor");
                return;
            }

            Debug.Log($"MapAccessorRendererSafe: Map accessor created: {pluginBridge.GetHandleInfo(mapAccessor)}");

            // Generate and render the road mesh
            GenerateRoadMesh();
            isMapLoaded = true;

            Debug.Log("MapAccessorRendererSafe: OpenDRIVE map loaded and rendered successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"MapAccessorRendererSafe: Exception loading map: {e.Message}");
        }
    }

    private void GenerateRoadMesh()
    {
        try
        {
            Debug.Log("MapAccessorRendererSafe: Parsing OpenDRIVE file with C# parser...");

            // Parse the OpenDRIVE file directly
            List<OpenDriveRoad> roads = OpenDriveParser.ParseOpenDriveFile(mapFilePath);

            if (roads.Count == 0)
            {
                Debug.LogError("MapAccessorRendererSafe: No roads parsed from OpenDRIVE file");
                return;
            }

            Debug.Log($"MapAccessorRendererSafe: Parsed {roads.Count} roads from OpenDRIVE");

            // Generate mesh from parsed road data
            Mesh roadMesh = OpenDriveGeometryGenerator.GenerateRoadMesh(roads, meshResolution);

            if (roadMesh == null || roadMesh.vertexCount == 0)
            {
                Debug.LogError("MapAccessorRendererSafe: Failed to generate mesh from road data");
                return;
            }

            Debug.Log($"MapAccessorRendererSafe: Generated mesh with {roadMesh.vertexCount} vertices, {roadMesh.triangles.Length/3} triangles");
            Debug.Log($"MapAccessorRendererSafe: Mesh bounds: {roadMesh.bounds}");

            // Create Unity mesh renderer
            CreateUnityMeshFromGenerated(roadMesh);

            Debug.Log("MapAccessorRendererSafe: OpenDRIVE road mesh generated successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"MapAccessorRendererSafe: Exception generating road mesh: {e.Message}");
        }
    }

    private bool ValidateMeshData(Vector3[] vertices, int[] indices)
    {
        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogError("MapAccessorRendererSafe: No vertices to validate");
            return false;
        }

        if (indices == null || indices.Length == 0)
        {
            Debug.LogError("MapAccessorRendererSafe: No indices to validate");
            return false;
        }

        if (indices.Length % 3 != 0)
        {
            Debug.LogError($"MapAccessorRendererSafe: Index count {indices.Length} is not divisible by 3");
            return false;
        }

        // Validate vertex coordinates
        const float MAX_COORDINATE = 100000.0f;
        int validVertexCount = 0;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];

            if (float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) ||
                float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z))
            {
                Debug.LogWarning($"MapAccessorRendererSafe: Invalid vertex at index {i}: {vertex}");
                vertices[i] = Vector3.zero; // Replace with safe default
                continue;
            }

            if (Mathf.Abs(vertex.x) > MAX_COORDINATE ||
                Mathf.Abs(vertex.y) > MAX_COORDINATE ||
                Mathf.Abs(vertex.z) > MAX_COORDINATE)
            {
                Debug.LogWarning($"MapAccessorRendererSafe: Vertex {i} exceeds coordinate limits: {vertex}");
                vertices[i] = Vector3.zero; // Replace with safe default
                continue;
            }

            validVertexCount++;
        }

        Debug.Log($"MapAccessorRendererSafe: Validated {validVertexCount}/{vertices.Length} vertices");

        // Validate indices
        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] < 0 || indices[i] >= vertices.Length)
            {
                Debug.LogError($"MapAccessorRendererSafe: Invalid index {indices[i]} at position {i} (vertex count: {vertices.Length})");
                return false;
            }
        }

        return validVertexCount > 0;
    }

    private void CreateUnityMesh(Vector3[] vertices, int[] indices)
    {
        Debug.Log("MapAccessorRendererSafe: Creating Unity mesh...");

        // Create mesh object
        Mesh mesh = new Mesh();
        mesh.name = meshName;

        // Assign vertices and indices
        mesh.vertices = vertices;
        mesh.triangles = indices;

        // Generate normals and bounds
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Debug.Log($"MapAccessorRendererSafe: Mesh created - Vertices: {mesh.vertexCount}, Triangles: {mesh.triangles.Length/3}");
        Debug.Log($"MapAccessorRendererSafe: Mesh bounds: {mesh.bounds}");

        // Find or create a GameObject to render the mesh
        GameObject roadObject = GameObject.Find("OpenDriveRoad");
        if (roadObject == null)
        {
            roadObject = new GameObject("OpenDriveRoad");
            Debug.Log("MapAccessorRendererSafe: Created new GameObject for road rendering");
        }

        // Add mesh components
        MeshFilter meshFilter = roadObject.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = roadObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = roadObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = roadObject.AddComponent<MeshRenderer>();

        // Assign mesh and material
        meshFilter.mesh = mesh;

        if (roadMaterial != null)
        {
            meshRenderer.material = roadMaterial;
            Debug.Log("MapAccessorRendererSafe: Applied custom road material");
        }
        else
        {
            // Create default material if none assigned
            Material defaultMaterial = new Material(Shader.Find("Standard"));
            defaultMaterial.color = Color.gray;
            meshRenderer.material = defaultMaterial;
            Debug.Log("MapAccessorRendererSafe: Applied default gray material");
        }

        // Add collider for physics
        MeshCollider meshCollider = roadObject.GetComponent<MeshCollider>();
        if (meshCollider == null) meshCollider = roadObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        Debug.Log("MapAccessorRendererSafe: Road mesh rendering setup complete");
    }

    private void OnDestroy()
    {
        if (mapAccessor != IntPtr.Zero && pluginBridge != null)
        {
            Debug.Log("MapAccessorRendererSafe: Cleaning up map accessor");
            pluginBridge.DestroyMapAccessor(mapAccessor);
            mapAccessor = IntPtr.Zero;
        }
    }

    // Public method to get map accessor for other components
    public IntPtr GetMapAccessor()
    {
        return mapAccessor;
    }

    // Check if map is loaded and ready
    public bool IsMapLoaded()
    {
        return isMapLoaded && mapAccessor != IntPtr.Zero;
    }

    // Reload the map
    public void ReloadMap()
    {
        if (mapAccessor != IntPtr.Zero && pluginBridge != null)
        {
            pluginBridge.DestroyMapAccessor(mapAccessor);
            mapAccessor = IntPtr.Zero;
        }

        isMapLoaded = false;
        LoadOpenDriveMap();
    }

    private void CreateUnityMeshFromGenerated(Mesh roadMesh)
    {
        Debug.Log("MapAccessorRendererSafe: Setting up mesh renderer for OpenDRIVE road network");

        // Find or create a GameObject to render the mesh
        GameObject roadObject = GameObject.Find("OpenDriveRoad");
        if (roadObject == null)
        {
            roadObject = new GameObject("OpenDriveRoad");
            Debug.Log("MapAccessorRendererSafe: Created new GameObject for OpenDRIVE road rendering");
        }

        // Add mesh components
        MeshFilter meshFilter = roadObject.GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = roadObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = roadObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = roadObject.AddComponent<MeshRenderer>();

        // Assign the generated mesh
        meshFilter.mesh = roadMesh;

        if (roadMaterial != null)
        {
            meshRenderer.material = roadMaterial;
            Debug.Log("MapAccessorRendererSafe: Applied custom road material");
        }
        else
        {
            // Try to load Road007 material automatically
            Material roadMaterial = RoadMaterialLoader.LoadRoadMaterial();

            if (roadMaterial != null)
            {
                meshRenderer.material = roadMaterial;
                Debug.Log($"MapAccessorRendererSafe: Applied road material: {roadMaterial.name}");
            }
            else
            {
                // Create a better default road material
                Material defaultRoadMaterial = RoadMaterialLoader.CreateDefaultRoadMaterial();
                meshRenderer.material = defaultRoadMaterial;
                Debug.Log("MapAccessorRendererSafe: Applied default road material");
            }
        }

        // Add collider for physics
        MeshCollider meshCollider = roadObject.GetComponent<MeshCollider>();
        if (meshCollider == null) meshCollider = roadObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = roadMesh;

        Debug.Log("MapAccessorRendererSafe: OpenDRIVE road network mesh rendering setup complete");
    }

    // Debug info
    public void LogMapInfo()
    {
        if (pluginBridge != null && mapAccessor != IntPtr.Zero)
        {
            Debug.Log($"MapAccessorRendererSafe: {pluginBridge.GetHandleInfo(mapAccessor)}");
        }
        else
        {
            Debug.Log("MapAccessorRendererSafe: No map loaded");
        }
    }

    // Callback for when native library is ready
    private void OnNativeLibraryReady()
    {
        Debug.Log("MapAccessorRendererSafe: Received native library ready callback");
        if (!isMapLoaded)
        {
            LoadOpenDriveMap();
        }
    }

    #if UNITY_EDITOR
    // Initialize available maps when the script is first loaded
    public void RefreshAvailableMaps()
    {
        if (!Directory.Exists("Assets/Maps/"))
        {
            availableMaps = new string[0];
            return;
        }

        string[] mapFiles = Directory.GetFiles("Assets/Maps/", "*.xodr", SearchOption.TopDirectoryOnly);
        availableMaps = mapFiles.Select(path => Path.GetFileName(path)).ToArray();

        // Try to maintain current selection if the map still exists
        string currentMapName = Path.GetFileName(mapFilePath);
        selectedMapIndex = System.Array.IndexOf(availableMaps, currentMapName);
        if (selectedMapIndex < 0) selectedMapIndex = 0;

        // Update the map file path
        if (availableMaps.Length > 0)
        {
            mapFilePath = "Assets/Maps/" + availableMaps[selectedMapIndex];
        }
    }

    public void SetSelectedMap(int index)
    {
        if (availableMaps != null && index >= 0 && index < availableMaps.Length)
        {
            selectedMapIndex = index;
            mapFilePath = "Assets/Maps/" + availableMaps[selectedMapIndex];
        }
    }

    private void OnValidate()
    {
        // Refresh maps whenever the component is loaded/changed in editor
        if (availableMaps == null || availableMaps.Length == 0)
        {
            RefreshAvailableMaps();
        }
    }
    #endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(MapAccessorRendererSafe))]
public class MapAccessorRendererSafeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MapAccessorRendererSafe mapRenderer = (MapAccessorRendererSafe)target;

        // Refresh button
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("OpenDRIVE Map Settings", EditorStyles.boldLabel);
        if (GUILayout.Button("Refresh Maps", GUILayout.Width(100)))
        {
            mapRenderer.RefreshAvailableMaps();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Map selection dropdown
        if (mapRenderer.availableMaps != null && mapRenderer.availableMaps.Length > 0)
        {
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Select Map", mapRenderer.selectedMapIndex, mapRenderer.availableMaps);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mapRenderer, "Change Map Selection");
                mapRenderer.SetSelectedMap(newIndex);
                EditorUtility.SetDirty(mapRenderer);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Selected Map Path:", mapRenderer.mapFilePath, EditorStyles.helpBox);
        }
        else
        {
            EditorGUILayout.HelpBox("No .xodr files found in Assets/Maps/\nClick 'Refresh Maps' to scan for available maps.", MessageType.Warning);
        }

        EditorGUILayout.Space();

        // Load map button
        if (GUILayout.Button("Load Selected Map", GUILayout.Height(30)))
        {
            if (Application.isPlaying)
            {
                mapRenderer.LoadOpenDriveMap();
            }
            else
            {
                EditorUtility.DisplayDialog("Load Map",
                    "Maps can only be loaded during play mode.\nEnter play mode first, then click this button.",
                    "OK");
            }
        }

        EditorGUILayout.Space();

        // Draw default inspector for other fields
        DrawDefaultInspector();
    }
}
#endif
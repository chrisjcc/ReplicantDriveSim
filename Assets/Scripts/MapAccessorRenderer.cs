using System;
using System.Runtime.InteropServices;
using UnityEngine;

[System.Serializable]
public struct VehicleState
{
    public double s;           // Longitudinal position along reference line
    public double t;           // Lateral offset from reference line
    public int roadId;         // Current road ID
    public int laneId;         // Current lane ID (negative for right lanes, positive for left)
    public double ds;          // Position within lane section
    public double dt;          // Lateral position within lane
    public double heading;     // Vehicle heading relative to lane direction (radians)
    public double laneWidth;   // Width of the current lane
    public bool isValid;       // Whether the vehicle state is valid
}

[System.Serializable]
public struct WorldPosition
{
    public double x;           // World X coordinate
    public double y;           // World Y coordinate  
    public double z;           // World Z coordinate
    public double heading;     // Heading in world coordinates (radians)
}

[System.Serializable]
public struct LaneInfo
{
    public int laneId;         // Lane ID
    public double width;       // Lane width at given s position
    public double centerOffset; // Offset to lane center from reference line
}

public class MapAccessorRenderer : MonoBehaviour
{
    [Header("OpenDRIVE Configuration")]
    [SerializeField] private string openDriveFilePath = "data.xodr";
    [SerializeField] public string terrainGameObjectName = "Terrain"; // Added public field for Inspector
    [SerializeField] private bool debugVehicleState = true;
    [SerializeField] private Transform vehicleTransform;
    
    [Header("Rendering")]
    [SerializeField] private Material roadMaterial;
    [SerializeField] private bool renderRoadMesh = true;
    
    private const string DllName = "libReplicantDriveSim";
    private IntPtr mapAccessor = IntPtr.Zero;
    private GameObject roadMeshObject;

    // Native function imports for MapAccessor
    [DllImport(DllName)]
    private static extern IntPtr CreateMapAccessor(string filePath);

    [DllImport(DllName)]
    private static extern void DestroyMapAccessor(IntPtr accessor);

    // Vehicle state derivation functions
    [DllImport(DllName)]
    private static extern IntPtr WorldToRoadCoordinates(IntPtr accessor, double x, double y, double z);

    [DllImport(DllName)]
    private static extern IntPtr RoadToWorldCoordinates(IntPtr accessor, double s, double t, int roadId);

    [DllImport(DllName)]
    private static extern void FreeVehicleState(IntPtr state);

    [DllImport(DllName)]
    private static extern void FreeWorldPosition(IntPtr position);

    // Road network query functions
    [DllImport(DllName)]
    private static extern IntPtr GetRoadIds(IntPtr accessor, out int roadCount);

    [DllImport(DllName)]
    private static extern IntPtr GetLanesAtPosition(IntPtr accessor, int roadId, double s, out int laneCount);

    [DllImport(DllName)]
    private static extern double GetRoadLength(IntPtr accessor, int roadId);

    [DllImport(DllName)]
    private static extern void FreeRoadIds(IntPtr roadIds);

    [DllImport(DllName)]
    private static extern void FreeLaneInfo(IntPtr laneInfo);

    // Validation functions
    [DllImport(DllName)]
    private static extern bool IsPositionOnRoad(IntPtr accessor, double x, double y, double z);

    [DllImport(DllName)]
    private static extern double GetClosestRoadDistance(IntPtr accessor, double x, double y, double z);

    // Mesh rendering functions
    [DllImport(DllName, EntryPoint = "Map_GetRoadVertices")]
    private static extern IntPtr GetRoadVertices(IntPtr accessor, out int vertexCount);

    [DllImport(DllName, EntryPoint = "Map_GetRoadIndices")]
    private static extern IntPtr GetRoadIndices(IntPtr accessor, out int indexCount);

    [DllImport(DllName, EntryPoint = "Map_FreeVertices")]
    private static extern void FreeVertices(IntPtr vertices);

    [DllImport(DllName, EntryPoint = "Map_FreeIndices")]
    private static extern void FreeIndices(IntPtr indices);

    void Awake()
    {
        InitializeMapAccessor();
    }

    void Start()
    {
        Debug.Log($"[MapAccessorRenderer] State check: renderRoadMesh={renderRoadMesh}, mapAccessor={(long)mapAccessor}");
        
        if (renderRoadMesh && mapAccessor != IntPtr.Zero)
        {
            RenderRoadMesh();
        }
        else
        {
            Debug.LogWarning("[MapAccessorRenderer] Skipping RenderRoadMesh because renderRoadMesh is false or mapAccessor is null.");
        }

        CenterTerrain();
    }

    private void CenterTerrain()
    {
        Terrain activeTerrain = Terrain.activeTerrain;
        if (activeTerrain != null)
        {
            Vector3 size = activeTerrain.terrainData.size;
            activeTerrain.transform.position = new Vector3(-size.x / 2, 0, -size.z / 2);
            Debug.Log($"Terrain centered at {activeTerrain.transform.position} with size {size}");
        }
        else
        {
            // Fallback: Try finding GameObject named by the user or common defaults
            GameObject terrainObj = GameObject.Find(terrainGameObjectName);
            if (terrainObj == null) terrainObj = GameObject.Find("Terrain");
            if (terrainObj == null) terrainObj = GameObject.Find("Ground");
            if (terrainObj == null) {
                 Terrain t = FindFirstObjectByType<Terrain>();
                 if (t != null) terrainObj = t.gameObject;
            }
            
            if (terrainObj != null)
            {
                Renderer r = terrainObj.GetComponent<Renderer>();
                if (r != null)
                {
                     Vector3 size = r.bounds.size;
                     // Assume pivot is center or try to center bounds? 
                     // Usually we want bounds center to be 0,0.
                     // If pivot is center, setting position to 0,0 centers it.
                     // If pivot is corner, we need offset.
                     // Let's assume we want to center the BOUNDS at 0,0,0 (on XZ)
                     
                     // Move object so that bounds.center.x = 0, bounds.center.z = 0
                     Vector3 currentCenter = r.bounds.center;
                     Vector3 offset = Vector3.zero - new Vector3(currentCenter.x, 0, currentCenter.z);
                     
                     terrainObj.transform.position += offset;
                     
                     Debug.Log($"Centered Terrain GameObject '{terrainObj.name}' (using Bounds) at {terrainObj.transform.position}");
                }
                else
                {
                     Debug.LogWarning($"Found GameObject '{terrainObj.name}' but it has no Renderer to calculate bounds.");
                }
            }
            else
            {
                Debug.LogInfo("No terrain found - this is expected if your scene uses OpenDRIVE road geometry instead of Unity terrain.");
            }
        }
    }

    void Update()
    {
        // Update vehicle state if vehicle transform is assigned
        if (vehicleTransform != null && mapAccessor != IntPtr.Zero && debugVehicleState)
        {
            UpdateVehicleState();
        }
    }

    void OnDestroy()
    {
        CleanupMapAccessor();
    }

    private void InitializeMapAccessor()
    {
        // Construct full path to OpenDRIVE file
        string fullPath = System.IO.Path.Combine(Application.dataPath, "Maps", openDriveFilePath);
        
        if (!System.IO.File.Exists(fullPath))
        {
            Debug.LogError($"OpenDRIVE file not found: {fullPath}");
            return;
        }

        Debug.Log($"Loading OpenDRIVE map: {fullPath}");
        
        try
        {
            mapAccessor = CreateMapAccessor(fullPath);
            
            if (mapAccessor == IntPtr.Zero)
            {
                Debug.LogError("Failed to create MapAccessor");
                return;
            }
            
            Debug.Log("MapAccessor initialized successfully");
            
            // Log road network information
            LogRoadNetworkInfo();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error initializing MapAccessor: {e.Message}");
        }
    }

    private void CleanupMapAccessor()
    {
        if (mapAccessor != IntPtr.Zero)
        {
            DestroyMapAccessor(mapAccessor);
            mapAccessor = IntPtr.Zero;
        }
        
        if (roadMeshObject != null)
        {
            DestroyImmediate(roadMeshObject);
        }
    }

    private void RenderRoadMesh()
    {
        if (mapAccessor == IntPtr.Zero) return;

        try
        {
            // Get road vertices
            int vertexCount;
            IntPtr verticesPtr = GetRoadVertices(mapAccessor, out vertexCount);
            
            if (verticesPtr == IntPtr.Zero || vertexCount <= 0)
            {
                Debug.LogError("Failed to get road vertices");
                return;
            }

            // Get road indices
            int indexCount;
            IntPtr indicesPtr = GetRoadIndices(mapAccessor, out indexCount);
            
            if (indicesPtr == IntPtr.Zero || indexCount <= 0)
            {
                Debug.LogError("Failed to get road indices");
                FreeVertices(verticesPtr);
                return;
            }

            // Copy native arrays to managed arrays
            float[] vertices = new float[vertexCount];
            int[] triangles = new int[indexCount];
            
            Marshal.Copy(verticesPtr, vertices, 0, vertexCount);
            Marshal.Copy(indicesPtr, triangles, 0, indexCount);

            // Create Unity mesh
            Mesh mesh = new Mesh();
            Vector3[] unityVertices = new Vector3[vertexCount / 3];

            // Transform from OpenDRIVE coordinates to Unity coordinates
            // OpenDRIVE: X=east, Y=north, Z=up
            // Unity: X=right, Y=up, Z=forward
            float yOffset = 0.2f; // Lift road slightly above terrain to avoid z-fighting

            for (int i = 0; i < unityVertices.Length; i++)
            {
                unityVertices[i] = new Vector3(
                    vertices[i * 3 + 0],               // X (east) -> X (right)
                    vertices[i * 3 + 2] + yOffset,     // Z (up) -> Y (up) + Offset
                    -vertices[i * 3 + 1]               // -Y (north) -> -Z (back to align)
                );
            }

            mesh.vertices = unityVertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Create road mesh GameObject
            roadMeshObject = new GameObject("OpenDRIVE Road Mesh");
            MeshFilter meshFilter = roadMeshObject.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = roadMeshObject.AddComponent<MeshRenderer>();
            
            meshFilter.mesh = mesh;
            meshRenderer.material = roadMaterial != null ? roadMaterial : CreateDefaultRoadMaterial();

            Debug.Log($"Road mesh created Successfully!\n" +
                     $" - Vertices: {unityVertices.Length}\n" +
                     $" - Triangles: {triangles.Length / 3}\n" +
                     $" - Bounds: {mesh.bounds}\n" +
                     $" - Material: {meshRenderer.material.name}");

            // Cleanup native memory
            FreeVertices(verticesPtr);
            FreeIndices(indicesPtr);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error rendering road mesh: {e.Message}");
        }
    }

    private Material CreateDefaultRoadMaterial()
    {
        Material material = new Material(Shader.Find("Standard"));
        material.color = Color.gray;
        material.SetFloat("_Metallic", 0.0f);
        material.SetFloat("_Smoothness", 0.3f);
        return material;
    }

    private void UpdateVehicleState()
    {
        if (vehicleTransform == null || mapAccessor == IntPtr.Zero) return;

        Vector3 vehiclePos = vehicleTransform.position;
        
        // Convert Unity coordinates to OpenDRIVE coordinates for query
        // Unity: X=right, Y=up, Z=forward
        // OpenDRIVE: X=east, Y=north, Z=up
        double openDriveX = vehiclePos.x;      // X remains X
        double openDriveY = -vehiclePos.z;     // Z becomes -Y (forward becomes north)
        double openDriveZ = vehiclePos.y;      // Y becomes Z (up remains up)

        try
        {
            IntPtr vehicleStatePtr = WorldToRoadCoordinates(mapAccessor, openDriveX, openDriveY, openDriveZ);
            
            if (vehicleStatePtr != IntPtr.Zero)
            {
                VehicleState state = Marshal.PtrToStructure<VehicleState>(vehicleStatePtr);
                
                if (state.isValid)
                {
                    Debug.Log($"Vehicle State - Road: {state.roadId}, Lane: {state.laneId}, " +
                             $"s: {state.s:F2}, t: {state.t:F2}, " +
                             $"Heading: {state.heading * Mathf.Rad2Deg:F1}°, " +
                             $"Lane Width: {state.laneWidth:F1}m");
                             
                    // Optional: Update vehicle transform based on road coordinates
                    // This could be used for lane-following behavior
                    UpdateVehicleFromRoadCoordinates(state);
                }
                else
                {
                    Debug.LogWarning("Vehicle is not on a valid road position");
                }
                
                FreeVehicleState(vehicleStatePtr);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error updating vehicle state: {e.Message}");
        }
    }

    private void UpdateVehicleFromRoadCoordinates(VehicleState state)
    {
        // Convert road coordinates back to world coordinates for verification
        try
        {
            IntPtr worldPosPtr = RoadToWorldCoordinates(mapAccessor, state.s, state.t, state.roadId);
            
            if (worldPosPtr != IntPtr.Zero)
            {
                WorldPosition worldPos = Marshal.PtrToStructure<WorldPosition>(worldPosPtr);
                
                // Convert back to Unity coordinates
                Vector3 unityPos = new Vector3(
                    (float)worldPos.x,      // X remains X
                    (float)worldPos.z,      // Z becomes Y
                    -(float)worldPos.y      // -Y becomes Z
                );
                
                // Optional: Snap vehicle to road (disabled by default for user control)
                // vehicleTransform.position = unityPos;
                
                // Optional: Update vehicle rotation to match road heading
                // float unityHeading = -(float)worldPos.heading * Mathf.Rad2Deg;
                // vehicleTransform.rotation = Quaternion.Euler(0, unityHeading, 0);
                
                FreeWorldPosition(worldPosPtr);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error converting road coordinates to world: {e.Message}");
        }
    }

    private void LogRoadNetworkInfo()
    {
        if (mapAccessor == IntPtr.Zero) return;

        try
        {
            int roadCount;
            IntPtr roadIdsPtr = GetRoadIds(mapAccessor, out roadCount);
            
            if (roadIdsPtr != IntPtr.Zero && roadCount > 0)
            {
                int[] roadIds = new int[roadCount];
                Marshal.Copy(roadIdsPtr, roadIds, 0, roadCount);
                
                Debug.Log($"Loaded OpenDRIVE map with {roadCount} roads");
                
                // Log info for first few roads
                for (int i = 0; i < Mathf.Min(roadCount, 5); i++)
                {
                    int roadId = roadIds[i];
                    double roadLength = GetRoadLength(mapAccessor, roadId);
                    
                    Debug.Log($"Road {roadId}: Length = {roadLength:F1}m");
                    
                    // Log lane info at road start
                    int laneCount;
                    IntPtr laneInfoPtr = GetLanesAtPosition(mapAccessor, roadId, 0.0, out laneCount);
                    
                    if (laneInfoPtr != IntPtr.Zero && laneCount > 0)
                    {
                        Debug.Log($"Road {roadId} has {laneCount} lanes at start");
                        FreeLaneInfo(laneInfoPtr);
                    }
                }
                
                FreeRoadIds(roadIdsPtr);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error logging road network info: {e.Message}");
        }
    }

    // Public API methods for external use
    public VehicleState? GetVehicleState(Vector3 worldPosition)
    {
        if (mapAccessor == IntPtr.Zero) return null;

        try
        {
            // Convert Unity to OpenDRIVE coordinates
            double openDriveX = worldPosition.x;
            double openDriveY = -worldPosition.z;
            double openDriveZ = worldPosition.y;

            IntPtr statePtr = WorldToRoadCoordinates(mapAccessor, openDriveX, openDriveY, openDriveZ);
            
            if (statePtr != IntPtr.Zero)
            {
                VehicleState state = Marshal.PtrToStructure<VehicleState>(statePtr);
                FreeVehicleState(statePtr);
                
                if (state.isValid)
                {
                    return state;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting vehicle state: {e.Message}");
        }
        
        return null;
    }

    public Vector3? GetWorldPosition(double s, double t, int roadId)
    {
        if (mapAccessor == IntPtr.Zero) return null;

        try
        {
            IntPtr worldPosPtr = RoadToWorldCoordinates(mapAccessor, s, t, roadId);
            
            if (worldPosPtr != IntPtr.Zero)
            {
                WorldPosition worldPos = Marshal.PtrToStructure<WorldPosition>(worldPosPtr);
                FreeWorldPosition(worldPosPtr);
                
                // Convert to Unity coordinates
                return new Vector3(
                    (float)worldPos.x,
                    (float)worldPos.z,
                    -(float)worldPos.y
                );
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error getting world position: {e.Message}");
        }
        
        return null;
    }

    public bool IsOnRoad(Vector3 worldPosition)
    {
        if (mapAccessor == IntPtr.Zero) return false;

        try
        {
            double openDriveX = worldPosition.x;
            double openDriveY = -worldPosition.z;
            double openDriveZ = worldPosition.y;
            
            return IsPositionOnRoad(mapAccessor, openDriveX, openDriveY, openDriveZ);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error checking if position is on road: {e.Message}");
            return false;
        }
    }

    public IntPtr GetMapAccessor()
    {
        return mapAccessor;
    }
}
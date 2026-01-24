using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Data structures for storing native simulation data without dynamic types
[System.Serializable]
public class TrafficData
{
    public int numAgents;
    public uint seed;
    public bool isInitialized;
    public bool hasMap;
    public IntPtr mapHandle;

    // Agent state data
    public Vector3[] agentPositions;
    public Vector3[] agentVelocities;
    public float[] agentYaws;
    public float simulationTime;

    public TrafficData()
    {
        agentPositions = new Vector3[0];
        agentVelocities = new Vector3[0];
        agentYaws = new float[0];
        simulationTime = 0f;
    }
}

[System.Serializable]
public class MapData
{
    public string filePath;
    public bool isLoaded;
    public int vertexCount;
    public int indexCount;
}

// Unity Plugin Bridge - P/Invoke-free solution for native library integration
// Uses Unity's SendMessage system for communication with native plugins
public class UnityPluginBridge : MonoBehaviour
{
    public static UnityPluginBridge Instance { get; private set; }

    private Dictionary<string, object> nativeResults = new Dictionary<string, object>();
    private Queue<string> pendingCallbacks = new Queue<string>();

    [Header("OpenDRIVE Settings")]
    public string mapFilePath = "Assets/Maps/data.xodr";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Debug.Log("UnityPluginBridge: Initializing P/Invoke-free native integration");

        // Initialize dummy implementations for now
        InitializeDummySystem();
    }

    private void InitializeDummySystem()
    {
        Debug.Log("UnityPluginBridge: Setting up dummy native library simulation");

        // Simulate native library initialization
        StartCoroutine(SimulateNativeLibraryLoad());
    }

    private IEnumerator SimulateNativeLibraryLoad()
    {
        yield return new WaitForSeconds(0.1f); // Simulate loading time

        Debug.Log("UnityPluginBridge: Native libraries simulated successfully");

        // Notify other systems that native integration is ready
        BroadcastMessage("OnNativeLibraryReady", SendMessageOptions.DontRequireReceiver);
    }

    // Simulation methods that replace P/Invoke calls
    public IntPtr CreateTrafficSimulation(int numAgents, uint seed)
    {
        Debug.Log($"UnityPluginBridge: Creating traffic simulation ({numAgents} agents, seed {seed})");

        // Generate a fake but consistent pointer
        int hash = (numAgents.GetHashCode() ^ ((int)seed).GetHashCode()) & 0x7FFFFFFF;
        if (hash == 0) hash = 0x12345678;

        IntPtr result = new IntPtr(hash);
        var trafficData = new TrafficData {
            numAgents = numAgents,
            seed = seed,
            isInitialized = false,
            hasMap = false,
            mapHandle = IntPtr.Zero
        };
        nativeResults[$"traffic_{result}"] = trafficData;

        Debug.Log($"UnityPluginBridge: Traffic simulation created with handle: {result}");
        return result;
    }

    public void DestroyTrafficSimulation(IntPtr traffic)
    {
        if (traffic == IntPtr.Zero) return;

        Debug.Log($"UnityPluginBridge: Destroying traffic simulation: {traffic}");

        string key = $"traffic_{traffic}";
        if (nativeResults.ContainsKey(key))
        {
            nativeResults.Remove(key);
            Debug.Log("UnityPluginBridge: Traffic simulation destroyed successfully");
        }
    }

    public IntPtr CreateMapAccessor(string filePath)
    {
        Debug.Log($"UnityPluginBridge: Creating map accessor from: {filePath}");

        // Check if file exists
        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogError($"UnityPluginBridge: Map file not found: {filePath}");
            return IntPtr.Zero;
        }

        // Generate a fake but consistent pointer based on file path
        int hash = filePath.GetHashCode() & 0x7FFFFFFF;
        if (hash == 0) hash = unchecked((int)0x87654321);

        IntPtr result = new IntPtr(hash);
        var mapData = new MapData {
            filePath = filePath,
            isLoaded = true,
            vertexCount = 6,
            indexCount = 6
        };
        nativeResults[$"map_{result}"] = mapData;

        Debug.Log($"UnityPluginBridge: Map accessor created with handle: {result}");
        return result;
    }

    public void DestroyMapAccessor(IntPtr accessor)
    {
        if (accessor == IntPtr.Zero) return;

        Debug.Log($"UnityPluginBridge: Destroying map accessor: {accessor}");

        string key = $"map_{accessor}";
        if (nativeResults.ContainsKey(key))
        {
            nativeResults.Remove(key);
            Debug.Log("UnityPluginBridge: Map accessor destroyed successfully");
        }
    }

    public void AssignMapToTraffic(IntPtr traffic, IntPtr mapAccessor)
    {
        if (traffic == IntPtr.Zero || mapAccessor == IntPtr.Zero)
        {
            Debug.LogError("UnityPluginBridge: Invalid handles for AssignMapToTraffic");
            return;
        }

        Debug.Log($"UnityPluginBridge: Assigning map {mapAccessor} to traffic {traffic}");

        string trafficKey = $"traffic_{traffic}";
        string mapKey = $"map_{mapAccessor}";

        if (nativeResults.ContainsKey(trafficKey) && nativeResults.ContainsKey(mapKey))
        {
            // Update traffic simulation to indicate map is assigned
            var trafficData = (TrafficData)nativeResults[trafficKey];
            trafficData.hasMap = true;
            trafficData.mapHandle = mapAccessor;
            nativeResults[trafficKey] = trafficData;

            Debug.Log("UnityPluginBridge: Map assigned successfully to traffic simulation");
        }
    }

    public void InitializeTrafficAgents(IntPtr traffic)
    {
        if (traffic == IntPtr.Zero) return;

        Debug.Log($"UnityPluginBridge: Initializing agents for traffic: {traffic}");

        string trafficKey = $"traffic_{traffic}";
        if (nativeResults.ContainsKey(trafficKey))
        {
            var trafficData = (TrafficData)nativeResults[trafficKey];
            trafficData.isInitialized = true;

            // Initialize agent state arrays
            InitializeAgentStates(trafficData);

            nativeResults[trafficKey] = trafficData;

            Debug.Log("UnityPluginBridge: Traffic agents initialized successfully");
        }
    }

    // Simulate mesh data retrieval
    public Vector3[] GetMeshVertices(IntPtr accessor, out int vertexCount)
    {
        vertexCount = 0;
        if (accessor == IntPtr.Zero) return new Vector3[0];

        Debug.Log($"UnityPluginBridge: Getting mesh vertices from accessor: {accessor}");

        string key = $"map_{accessor}";
        if (nativeResults.ContainsKey(key))
        {
            // Generate simple test geometry - a small road segment
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-5f, 0f, -10f),  // Bottom left
                new Vector3(5f, 0f, -10f),   // Bottom right
                new Vector3(-5f, 0f, 10f),   // Top left
                new Vector3(5f, 0f, 10f),    // Top right
                new Vector3(-5f, 0f, 30f),   // Extended top left
                new Vector3(5f, 0f, 30f)     // Extended top right
            };

            vertexCount = vertices.Length;
            Debug.Log($"UnityPluginBridge: Generated {vertexCount} test vertices");
            return vertices;
        }

        Debug.LogError($"UnityPluginBridge: Map accessor {accessor} not found");
        return new Vector3[0];
    }

    public int[] GetMeshIndices(IntPtr accessor, out int indexCount)
    {
        indexCount = 0;
        if (accessor == IntPtr.Zero) return new int[0];

        Debug.Log($"UnityPluginBridge: Getting mesh indices from accessor: {accessor}");

        string key = $"map_{accessor}";
        if (nativeResults.ContainsKey(key))
        {
            // Generate indices for two triangles forming a quad road segment
            int[] indices = new int[]
            {
                0, 2, 1,  // First triangle
                1, 2, 3,  // Second triangle
                2, 4, 3,  // Third triangle
                3, 4, 5   // Fourth triangle
            };

            indexCount = indices.Length;
            Debug.Log($"UnityPluginBridge: Generated {indexCount} test indices");
            return indices;
        }

        Debug.LogError($"UnityPluginBridge: Map accessor {accessor} not found");
        return new int[0];
    }

    // Utility method to check if handles are valid
    public bool IsValidHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;

        string trafficKey = $"traffic_{handle}";
        string mapKey = $"map_{handle}";

        return nativeResults.ContainsKey(trafficKey) || nativeResults.ContainsKey(mapKey);
    }

    // Get information about a handle
    public string GetHandleInfo(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return "NULL";

        string trafficKey = $"traffic_{handle}";
        string mapKey = $"map_{handle}";

        if (nativeResults.ContainsKey(trafficKey))
        {
            var data = (TrafficData)nativeResults[trafficKey];
            return $"Traffic(agents:{data.numAgents}, initialized:{data.isInitialized}, hasMap:{data.hasMap})";
        }

        if (nativeResults.ContainsKey(mapKey))
        {
            var data = (MapData)nativeResults[mapKey];
            return $"Map(path:{data.filePath}, loaded:{data.isLoaded})";
        }

        return "Unknown";
    }

    private void InitializeAgentStates(TrafficData trafficData)
    {
        int numAgents = trafficData.numAgents;
        trafficData.agentPositions = new Vector3[numAgents];
        trafficData.agentVelocities = new Vector3[numAgents];
        trafficData.agentYaws = new float[numAgents];

        // Get road network for spawning agents on actual roads
        List<OpenDriveRoad> roads = null;
        MapData mapData = null;

        // Find the map data
        foreach (var kvp in nativeResults)
        {
            if (kvp.Key.StartsWith("map_") && kvp.Value is MapData)
            {
                mapData = (MapData)kvp.Value;
                break;
            }
        }

        if (mapData != null)
        {
            // Parse road data for agent spawning
            roads = OpenDriveParser.ParseOpenDriveFile(mapData.filePath);
            Debug.Log($"UnityPluginBridge: Found {roads?.Count ?? 0} roads for agent spawning");

            // Debug road information
            if (roads != null && roads.Count > 0)
            {
                for (int r = 0; r < Mathf.Min(roads.Count, 3); r++)
                {
                    var road = roads[r];
                    Debug.Log($"UnityPluginBridge: Road {r} - ID: {road.id}, Length: {road.length:F2}m, Geometries: {road.geometries?.Count ?? 0}");
                }
            }
        }

        // Initialize each agent at road positions (similar to C++ simulation logic)
        System.Random random = new System.Random((int)trafficData.seed);

        // Get valid road spawn points first
        List<Vector3> validSpawnPoints = new List<Vector3>();
        List<float> validSpawnYaws = new List<float>();

        if (roads != null && roads.Count > 0)
        {
            // Generate multiple spawn candidates and pick the best ones
            for (int attempt = 0; attempt < numAgents * 10; attempt++) // Generate 10x more candidates
            {
                Vector3 position = GetRandomRoadPosition(roads, random);
                float yaw = GetRoadHeadingAtPosition(roads, position, random);

                // Validate spawn point is actually on road surface
                bool isValidSpawn = IsPositionOnRoad(position, roads);

                if (position != Vector3.zero && isValidSpawn) // Valid road position found
                {
                    validSpawnPoints.Add(position);
                    validSpawnYaws.Add(yaw);

                    if (validSpawnPoints.Count >= numAgents) break; // Got enough spawn points
                }
                else if (position != Vector3.zero)
                {
                    Debug.LogWarning($"UnityPluginBridge: Generated position {position} failed road validation");
                }
            }
            Debug.Log($"UnityPluginBridge: Generated {validSpawnPoints.Count} valid road spawn points for {numAgents} agents");
        }

        for (int i = 0; i < numAgents; i++)
        {
            Vector3 position;
            float yaw;

            if (i < validSpawnPoints.Count)
            {
                // Use valid road spawn point
                position = validSpawnPoints[i];
                yaw = validSpawnYaws[i];
                // Double-check validation for final confirmation
                bool isOnRoad = IsPositionOnRoad(position, roads);
                string status = isOnRoad ? "ON ROAD" : "OFF ROAD";
                Debug.Log($"UnityPluginBridge: Agent {i} spawned {status} at {position} with yaw {yaw:F1}°");
            }
            else
            {
                // Fallback: spawn on first road if available, otherwise use safe fallback
                if (roads != null && roads.Count > 0 && roads[0].geometries.Count > 0)
                {
                    var geom = roads[0].geometries[0];
                    float spacing = i * 5f; // Space agents along first road
                    position = new Vector3(geom.x + spacing, 0f, geom.y);
                    yaw = geom.hdg * Mathf.Rad2Deg;
                    Debug.LogWarning($"UnityPluginBridge: Agent {i} spawned on FALLBACK road position at {position}");
                }
                else
                {
                    // Final fallback: spawn around origin (off-road)
                    float angle = (float)i / numAgents * 2f * Mathf.PI;
                    position = new Vector3(
                        Mathf.Cos(angle) * 5f, // Smaller radius
                        0f,
                        Mathf.Sin(angle) * 5f
                    );
                    yaw = angle * Mathf.Rad2Deg;
                    Debug.LogError($"UnityPluginBridge: Agent {i} spawned OFF-ROAD (no road data) at {position}");
                }
            }

            trafficData.agentPositions[i] = position;
            trafficData.agentVelocities[i] = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad)) * 5f;
            trafficData.agentYaws[i] = yaw;
        }
    }

    private Vector3 GetRandomRoadPosition(List<OpenDriveRoad> roads, System.Random random)
    {
        if (roads.Count == 0) return Vector3.zero;

        // Try multiple roads to find a valid spawn point
        for (int roadAttempt = 0; roadAttempt < 10; roadAttempt++)
        {
            // Pick a random road
            OpenDriveRoad road = roads[random.Next(roads.Count)];

            // Skip very short roads (less than 5 meters)
            if (road.length < 5.0f) continue;

            // Pick a random position along the road (avoid very start/end)
            float minS = road.length * 0.1f; // Start 10% along the road
            float maxS = road.length * 0.9f; // End 90% along the road
            float s = minS + (float)random.NextDouble() * (maxS - minS);

            // Get position using proper OpenDRIVE coordinate transformation
            Vector3 centerlinePoint = GetPointAtS(road, s);
            if (centerlinePoint == Vector3.zero) continue;

            // Add random lateral offset within road width (stay within road boundaries)
            float roadWidth = GetRoadWidthAtS(road, s);
            float lateralOffset = ((float)random.NextDouble() - 0.5f) * roadWidth * 0.6f; // Use 60% of road width for safety

            Vector3 heading = GetHeadingAtS(road, s);
            Vector3 lateral = new Vector3(-heading.z, 0f, heading.x); // Perpendicular to heading

            Vector3 finalPosition = centerlinePoint + lateral * lateralOffset;
            finalPosition.y = 0.5f; // Vehicle height above road surface

            Debug.Log($"UnityPluginBridge: Generated spawn point at road {road.id}, s={s:F2}, lateral={lateralOffset:F2}, pos={finalPosition}");
            return finalPosition;
        }

        Debug.LogWarning("UnityPluginBridge: Failed to find valid road spawn point after 10 attempts");
        return Vector3.zero;
    }

    // Public method to get a random road position for dynamic target selection
    public Vector3 GetRandomRoadPosition()
    {
        // Find roads from map data
        List<OpenDriveRoad> roads = null;

        // Try to get roads from current map data
        foreach (var kvp in nativeResults)
        {
            if (kvp.Key.StartsWith("map_") && kvp.Value is MapData mapData)
            {
                // Parse road data from the map file
                roads = OpenDriveParser.ParseOpenDriveFile(mapData.filePath);
                break;
            }
        }

        if (roads == null || roads.Count == 0)
        {
            Debug.LogWarning("UnityPluginBridge: No road data available for random position");
            return Vector3.zero;
        }

        // Use the same logic as spawn point generation
        System.Random random = new System.Random();
        return GetRandomRoadPosition(roads, random);
    }

    private float GetRoadHeadingAtPosition(List<OpenDriveRoad> roads, Vector3 position, System.Random random)
    {
        // Find the closest road and return its heading
        float closestDistance = float.MaxValue;
        float bestHeading = 0f;

        foreach (var road in roads)
        {
            foreach (var geom in road.geometries)
            {
                Vector3 roadPoint = new Vector3(geom.x, 0.5f, geom.y);
                float distance = Vector3.Distance(position, roadPoint);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestHeading = geom.hdg * Mathf.Rad2Deg; // Convert to degrees
                }
            }
        }

        return bestHeading;
    }

    private Vector3 GetPointAtS(OpenDriveRoad road, float s)
    {
        // Use the same coordinate transformation as OpenDriveGeometryGenerator
        // to ensure spawn points match the actual road mesh

        // Find the geometry segment that contains this s value
        OpenDriveGeometry currentGeom = null;
        foreach (var geom in road.geometries)
        {
            if (s >= geom.s && s <= geom.s + geom.length)
            {
                currentGeom = geom;
                break;
            }
        }

        if (currentGeom == null)
        {
            // Use the last geometry if s is beyond the road
            if (road.geometries.Count > 0)
            {
                currentGeom = road.geometries[road.geometries.Count - 1];
                s = currentGeom.s + currentGeom.length;
            }
            else
            {
                return Vector3.zero;
            }
        }

        // Calculate local s within this geometry
        float localS = s - currentGeom.s;

        // Get point using proper OpenDRIVE coordinate transformation (same as mesh generation)
        Vector2 localPoint = GetLocalPointInGeometry(currentGeom, localS);
        Vector2 worldPoint = TransformToWorldCoordinates(localPoint, currentGeom);

        // Convert OpenDRIVE coordinates to Unity coordinates (same as mesh)
        // OpenDRIVE: X=east, Y=north, Z=up
        // Unity: X=right, Y=up, Z=forward
        return new Vector3(worldPoint.x, 0.1f, worldPoint.y);
    }

    private Vector2 GetLocalPointInGeometry(OpenDriveGeometry geom, float s)
    {
        switch (geom.type)
        {
            case GeometryType.Line:
                return new Vector2(s, 0f);

            case GeometryType.Arc:
                return GetArcPoint(s, geom.curvature);

            case GeometryType.Spiral:
                return GetSpiralPoint(s, geom.curvStart, geom.curvEnd, geom.length);

            default:
                return new Vector2(s, 0f);
        }
    }

    private Vector2 GetArcPoint(float s, float curvature)
    {
        if (Mathf.Abs(curvature) < 1e-10f)
        {
            // Straight line case
            return new Vector2(s, 0f);
        }

        float radius = 1f / curvature;
        float theta = s * curvature;

        return new Vector2(
            radius * Mathf.Sin(theta),
            radius * (1f - Mathf.Cos(theta))
        );
    }

    private Vector2 GetSpiralPoint(float s, float curvStart, float curvEnd, float length)
    {
        // Simplified spiral calculation using Fresnel integrals approximation
        float curvRate = (curvEnd - curvStart) / length;

        Vector2 position = Vector2.zero;
        float heading = curvStart * s;

        // Use small steps for integration
        int steps = Mathf.Max(1, (int)(s / 0.1f));
        float stepSize = s / steps;

        for (int i = 0; i < steps; i++)
        {
            float stepS = i * stepSize;
            float stepCurvature = curvStart + curvRate * stepS;

            position.x += stepSize * Mathf.Cos(heading);
            position.y += stepSize * Mathf.Sin(heading);

            heading += stepCurvature * stepSize;
        }

        return position;
    }

    private Vector2 TransformToWorldCoordinates(Vector2 localPoint, OpenDriveGeometry geom)
    {
        // Rotate by heading
        float cos_hdg = Mathf.Cos(geom.hdg);
        float sin_hdg = Mathf.Sin(geom.hdg);

        Vector2 rotated = new Vector2(
            localPoint.x * cos_hdg - localPoint.y * sin_hdg,
            localPoint.x * sin_hdg + localPoint.y * cos_hdg
        );

        // Translate to world position
        return new Vector2(geom.x + rotated.x, geom.y + rotated.y);
    }

    private Vector3 GetHeadingAtS(OpenDriveRoad road, float s)
    {
        // Find the geometry segment for heading calculation
        float currentS = 0f;
        foreach (var geom in road.geometries)
        {
            if (currentS + geom.length >= s)
            {
                return new Vector3(Mathf.Cos(geom.hdg), 0f, Mathf.Sin(geom.hdg));
            }
            currentS += geom.length;
        }

        // Fallback: use first geometry heading
        if (road.geometries.Count > 0)
        {
            var geom = road.geometries[0];
            return new Vector3(Mathf.Cos(geom.hdg), 0f, Mathf.Sin(geom.hdg));
        }

        return Vector3.forward;
    }

    private float GetRoadWidthAtS(OpenDriveRoad road, float s)
    {
        // Simplified: assume standard road width
        // In a full implementation, this would parse the road's lane sections
        return 7.0f; // Standard 2-lane road width in meters
    }

    private bool IsPositionOnRoad(Vector3 position, List<OpenDriveRoad> roads)
    {
        if (position == Vector3.zero) return false;

        // Check if position is within reasonable distance of any road centerline
        float minDistanceToRoad = float.MaxValue;
        bool foundNearbyRoad = false;

        foreach (var road in roads)
        {
            // Sample points along the road centerline
            for (float s = 0; s <= road.length; s += 2.0f) // Check every 2 meters
            {
                Vector3 roadPoint = GetPointAtS(road, s);
                if (roadPoint != Vector3.zero)
                {
                    float distance = Vector3.Distance(position, roadPoint);
                    minDistanceToRoad = Mathf.Min(minDistanceToRoad, distance);

                    // Check if position is within road width
                    float roadWidth = GetRoadWidthAtS(road, s);
                    if (distance <= roadWidth * 0.5f) // Within road width
                    {
                        foundNearbyRoad = true;
                        Debug.Log($"UnityPluginBridge: Position {position} validated ON ROAD (distance: {distance:F2}m from centerline)");
                        break;
                    }
                }
            }
            if (foundNearbyRoad) break;
        }

        if (!foundNearbyRoad)
        {
            Debug.LogWarning($"UnityPluginBridge: Position {position} is OFF ROAD (min distance: {minDistanceToRoad:F2}m from centerline)");
        }

        return foundNearbyRoad;
    }

    // Simulate traffic simulation step (replaces C++ Traffic::step)
    public void StepTrafficSimulation(IntPtr traffic, float deltaTime)
    {
        string trafficKey = $"traffic_{traffic}";
        if (!nativeResults.ContainsKey(trafficKey)) return;

        var trafficData = (TrafficData)nativeResults[trafficKey];
        if (!trafficData.isInitialized) return;

        trafficData.simulationTime += deltaTime;

        // Debug: Log simulation stepping every 120 frames
        if (Time.frameCount % 120 == 0)
        {
            Debug.Log($"UnityPluginBridge: Stepping simulation - {trafficData.numAgents} agents, simTime: {trafficData.simulationTime:F2}s, deltaTime: {deltaTime:F3}");
        }

        // C++ Traffic simulation logic (simplified version)
        for (int i = 0; i < trafficData.numAgents; i++)
        {
            Vector3 oldPos = trafficData.agentPositions[i];

            // Update position based on current velocity
            trafficData.agentPositions[i] += trafficData.agentVelocities[i] * deltaTime;

            // Debug: Log first agent position changes every 120 frames
            if (i == 0 && Time.frameCount % 120 == 0)
            {
                Debug.Log($"UnityPluginBridge: Agent 0 moved from {oldPos} to {trafficData.agentPositions[i]} (velocity: {trafficData.agentVelocities[i]})");
            }

            // Simple road-following behavior (placeholder for actual C++ Traffic::step logic)
            // This simulates the C++ traffic simulation dynamics without circular motion

            // Calculate forward movement along road direction
            Vector3 position = trafficData.agentPositions[i];
            Vector3 velocity = trafficData.agentVelocities[i];

            // Update velocity based on road direction and traffic rules
            // For now, maintain constant speed along current heading with slight road curvature
            float speed = Mathf.Max(8f, velocity.magnitude); // Maintain reasonable speed
            Vector3 forward = velocity.normalized;

            // Add slight curvature to simulate road following
            float roadCurvature = Mathf.Sin(trafficData.simulationTime * 0.3f + i) * 0.1f;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            forward += right * roadCurvature;
            forward = forward.normalized;

            trafficData.agentVelocities[i] = forward * speed;

            // Update yaw based on velocity direction
            if (trafficData.agentVelocities[i].magnitude > 0.1f)
            {
                trafficData.agentYaws[i] = Mathf.Atan2(
                    trafficData.agentVelocities[i].x,
                    trafficData.agentVelocities[i].z
                ) * Mathf.Rad2Deg;
            }
        }

        nativeResults[trafficKey] = trafficData;
    }

    // Get agent positions (replaces C++ Traffic::get_agent_positions)
    public Vector3[] GetAgentPositions(IntPtr traffic)
    {
        string trafficKey = $"traffic_{traffic}";
        if (nativeResults.ContainsKey(trafficKey))
        {
            var trafficData = (TrafficData)nativeResults[trafficKey];
            return trafficData.agentPositions ?? new Vector3[0];
        }
        return new Vector3[0];
    }

    // Get agent velocities (replaces C++ Traffic::get_agent_velocities)
    public Vector3[] GetAgentVelocities(IntPtr traffic)
    {
        string trafficKey = $"traffic_{traffic}";
        if (nativeResults.ContainsKey(trafficKey))
        {
            var trafficData = (TrafficData)nativeResults[trafficKey];
            return trafficData.agentVelocities ?? new Vector3[0];
        }
        return new Vector3[0];
    }

    // Get agent yaws (replaces C++ Traffic::get_agent_orientations)
    public float[] GetAgentYaws(IntPtr traffic)
    {
        string trafficKey = $"traffic_{traffic}";
        if (nativeResults.ContainsKey(trafficKey))
        {
            var trafficData = (TrafficData)nativeResults[trafficKey];
            return trafficData.agentYaws ?? new float[0];
        }
        return new float[0];
    }

    private void OnDestroy()
    {
        Debug.Log("UnityPluginBridge: Cleaning up all native handles");
        nativeResults.Clear();
        pendingCallbacks.Clear();
    }

    // Debug methods
    public void LogAllHandles()
    {
        Debug.Log($"UnityPluginBridge: Active handles ({nativeResults.Count}):");
        foreach (var kvp in nativeResults)
        {
            Debug.Log($"  {kvp.Key}: {kvp.Value}");
        }
    }
}
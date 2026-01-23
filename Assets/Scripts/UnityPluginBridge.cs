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
        }

        // Initialize each agent at road positions (similar to C++ simulation logic)
        System.Random random = new System.Random((int)trafficData.seed);

        for (int i = 0; i < numAgents; i++)
        {
            Vector3 position = Vector3.zero;
            float yaw = 0f;

            if (roads != null && roads.Count > 0)
            {
                // Spawn agents on actual road network
                position = GetRandomRoadPosition(roads, random);
                yaw = GetRoadHeadingAtPosition(roads, position, random);
            }
            else
            {
                // Fallback: spawn around origin
                float angle = (float)i / numAgents * 2f * Mathf.PI;
                position = new Vector3(
                    Mathf.Cos(angle) * 10f,
                    0.5f,
                    Mathf.Sin(angle) * 10f
                );
                yaw = angle * Mathf.Rad2Deg;
            }

            trafficData.agentPositions[i] = position;
            trafficData.agentVelocities[i] = Vector3.forward * 5f; // Initial forward velocity
            trafficData.agentYaws[i] = yaw;

            Debug.Log($"UnityPluginBridge: Initialized agent {i} at {position} with yaw {yaw}°");
        }
    }

    private Vector3 GetRandomRoadPosition(List<OpenDriveRoad> roads, System.Random random)
    {
        if (roads.Count == 0) return Vector3.zero;

        // Pick a random road
        OpenDriveRoad road = roads[random.Next(roads.Count)];

        // Pick a random position along the road
        float s = (float)random.NextDouble() * road.length;

        // Get the first geometry segment (simplified)
        if (road.geometries.Count > 0)
        {
            var geom = road.geometries[0];
            // Simple line approximation for now
            float t = s / road.length;
            Vector3 start = new Vector3(geom.x, 0.5f, geom.y);
            Vector3 end = start + new Vector3(
                Mathf.Cos(geom.hdg) * road.length,
                0f,
                Mathf.Sin(geom.hdg) * road.length
            );
            return Vector3.Lerp(start, end, t);
        }

        return Vector3.zero;
    }

    private float GetRoadHeadingAtPosition(List<OpenDriveRoad> roads, Vector3 position, System.Random random)
    {
        // Simplified: return a reasonable heading
        return (float)random.NextDouble() * 360f;
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
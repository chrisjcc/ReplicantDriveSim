using UnityEngine;

// Setup script to configure the scene with P/Invoke-free components
public class SafeSimulationSetup : MonoBehaviour
{
    [Header("Setup Options")]
    public bool autoSetupOnStart = true;
    public bool replaceExistingComponents = true;

    [Header("Simulation Settings")]
    public string mapFilePath = "Assets/Maps/data.xodr";
    public int numberOfAgents = 3;
    public uint randomSeed = 12345;

    void Start()
    {
        if (autoSetupOnStart)
        {
            Debug.Log("SafeSimulationSetup: Auto-setup enabled, configuring P/Invoke-free simulation");
            SetupSafeSimulation();
        }
    }

    [ContextMenu("Setup Safe Simulation")]
    public void SetupSafeSimulation()
    {
        Debug.Log("SafeSimulationSetup: Setting up P/Invoke-free simulation components");

        // 1. Setup UnityPluginBridge
        SetupPluginBridge();

        // 2. Setup MapAccessorRendererSafe
        SetupMapRenderer();

        // 3. Setup TrafficManagerSafe
        SetupTrafficManager();

        // 4. Disable old components if requested
        if (replaceExistingComponents)
        {
            DisableOldComponents();
        }

        Debug.Log("SafeSimulationSetup: P/Invoke-free simulation setup complete!");
    }

    private void SetupPluginBridge()
    {
        UnityPluginBridge bridge = FindFirstObjectByType<UnityPluginBridge>();
        if (bridge == null)
        {
            GameObject bridgeObj = new GameObject("UnityPluginBridge");
            bridge = bridgeObj.AddComponent<UnityPluginBridge>();
            Debug.Log("SafeSimulationSetup: Created UnityPluginBridge");
        }
        else
        {
            Debug.Log("SafeSimulationSetup: UnityPluginBridge already exists");
        }
    }

    private void SetupMapRenderer()
    {
        MapAccessorRendererSafe mapRenderer = FindFirstObjectByType<MapAccessorRendererSafe>();
        if (mapRenderer == null)
        {
            GameObject mapObj = GameObject.Find("MapRenderer");
            if (mapObj == null)
            {
                mapObj = new GameObject("MapRenderer");
            }

            mapRenderer = mapObj.AddComponent<MapAccessorRendererSafe>();
            mapRenderer.mapFilePath = mapFilePath;

            // Try to assign a default road material
            Material roadMaterial = Resources.Load<Material>("RoadMaterial");
            if (roadMaterial != null)
            {
                mapRenderer.roadMaterial = roadMaterial;
            }

            Debug.Log($"SafeSimulationSetup: Created MapAccessorRendererSafe with map: {mapFilePath}");
        }
        else
        {
            Debug.Log("SafeSimulationSetup: MapAccessorRendererSafe already exists");
            if (string.IsNullOrEmpty(mapRenderer.mapFilePath))
            {
                mapRenderer.mapFilePath = mapFilePath;
                Debug.Log($"SafeSimulationSetup: Updated map path to: {mapFilePath}");
            }
        }
    }

    private void SetupTrafficManager()
    {
        TrafficManagerSafe trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
        if (trafficManager == null)
        {
            GameObject trafficObj = GameObject.Find("TrafficManager");
            if (trafficObj == null)
            {
                trafficObj = new GameObject("TrafficManager");
            }

            trafficManager = trafficObj.AddComponent<TrafficManagerSafe>();
            trafficManager.numberOfAgents = numberOfAgents;
            trafficManager.randomSeed = randomSeed;

            // Create agent parent object
            GameObject agentParent = GameObject.Find("TrafficAgents");
            if (agentParent == null)
            {
                agentParent = new GameObject("TrafficAgents");
            }
            trafficManager.agentParent = agentParent.transform;

            Debug.Log($"SafeSimulationSetup: Created TrafficManagerSafe with {numberOfAgents} agents");
        }
        else
        {
            Debug.Log("SafeSimulationSetup: TrafficManagerSafe already exists");
            trafficManager.numberOfAgents = numberOfAgents;
            trafficManager.randomSeed = randomSeed;
        }
    }

    private void DisableOldComponents()
    {
        Debug.Log("SafeSimulationSetup: Disabling old P/Invoke components");

        // Note: Old P/Invoke components are already disabled/removed
        Debug.Log("SafeSimulationSetup: Skipping old component disable - P/Invoke components already removed");

        // Find and disable any other known P/Invoke components
        MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var component in allComponents)
        {
            string typeName = component.GetType().Name;
            if (typeName.Contains("PInvoke") ||
                typeName.Contains("Native") && !typeName.Contains("Safe") ||
                typeName == "TrafficAgent" && component.GetType() != typeof(TrafficAgentSafe))
            {
                component.enabled = false;
                Debug.Log($"SafeSimulationSetup: Disabled potentially unsafe component: {typeName}");
            }
        }
    }

    [ContextMenu("Reset Simulation")]
    public void ResetSimulation()
    {
        Debug.Log("SafeSimulationSetup: Resetting simulation");

        // Clean up existing safe components
        TrafficManagerSafe trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
        if (trafficManager != null)
        {
            trafficManager.RestartTrafficSimulation();
        }

        MapAccessorRendererSafe mapRenderer = FindFirstObjectByType<MapAccessorRendererSafe>();
        if (mapRenderer != null)
        {
            mapRenderer.ReloadMap();
        }

        Debug.Log("SafeSimulationSetup: Simulation reset complete");
    }

    [ContextMenu("Log System Status")]
    public void LogSystemStatus()
    {
        Debug.Log("=== SAFE SIMULATION STATUS ===");

        UnityPluginBridge bridge = FindFirstObjectByType<UnityPluginBridge>();
        Debug.Log($"UnityPluginBridge: {(bridge != null ? "Present" : "Missing")}");

        MapAccessorRendererSafe mapRenderer = FindFirstObjectByType<MapAccessorRendererSafe>();
        if (mapRenderer != null)
        {
            Debug.Log($"MapRenderer: Present, Map Loaded: {mapRenderer.IsMapLoaded()}");
            mapRenderer.LogMapInfo();
        }
        else
        {
            Debug.Log("MapRenderer: Missing");
        }

        TrafficManagerSafe trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
        if (trafficManager != null)
        {
            Debug.Log($"TrafficManager: Present, Initialized: {trafficManager.IsTrafficInitialized()}");
            trafficManager.LogTrafficInfo();
        }
        else
        {
            Debug.Log("TrafficManager: Missing");
        }

        // Count active agents
        TrafficAgentSafe[] agents = FindObjectsByType<TrafficAgentSafe>(FindObjectsSortMode.None);
        Debug.Log($"Active Agents: {agents.Length}");

        Debug.Log("=== END STATUS ===");
    }

    [ContextMenu("Clear All GameObjects")]
    public void ClearAllGameObjects()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("SafeSimulationSetup: Cannot clear GameObjects during play mode");
            return;
        }

        Debug.Log("SafeSimulationSetup: Clearing all simulation GameObjects");

        // Clean up road objects
        GameObject roadObj = GameObject.Find("OpenDriveRoad");
        if (roadObj != null) DestroyImmediate(roadObj);

        // Clean up traffic objects
        GameObject trafficParent = GameObject.Find("TrafficAgents");
        if (trafficParent != null) DestroyImmediate(trafficParent);

        Debug.Log("SafeSimulationSetup: Cleanup complete");
    }
}
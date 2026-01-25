using UnityEngine;

// Helper script to automatically setup the P/Invoke-free simulation when Unity starts
[System.Serializable]
public class SceneSetupHelper : MonoBehaviour
{
    [Header("Auto-Setup Configuration")]
    public bool setupOnAwake = true;
    public bool createTrafficAgents = true;
    public bool createMapRenderer = true;

    [Header("Simulation Settings")]
    public string mapFilePath = "Assets/Maps/data.xodr";
    public int numberOfAgents = 3;
    public uint randomSeed = 12345;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    void Awake()
    {
        if (setupOnAwake)
        {
            if (enableDebugLogs)
                Debug.Log("SceneSetupHelper: Auto-setting up P/Invoke-free simulation on scene load");

            // Delay setup slightly to ensure all GameObjects are initialized
            Invoke(nameof(SetupSafeSimulation), 0.1f);
        }
    }

    [ContextMenu("Setup Safe Simulation Now")]
    public void SetupSafeSimulation()
    {
        if (enableDebugLogs)
            Debug.Log("SceneSetupHelper: Starting P/Invoke-free simulation setup");

        try
        {
            // Step 1: Setup UnityPluginBridge
            SetupPluginBridge();

            // Step 2: Setup Map Renderer if requested
            if (createMapRenderer)
            {
                SetupMapRenderer();
            }

            // Step 3: Setup Traffic Manager if requested
            if (createTrafficAgents)
            {
                SetupTrafficManager();
            }

            // Step 4: Fix camera target references
            FixCameraReferences();

            if (enableDebugLogs)
                Debug.Log("SceneSetupHelper: P/Invoke-free simulation setup complete!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SceneSetupHelper: Error during setup: {e.Message}");
        }
    }

    private void SetupPluginBridge()
    {
        UnityPluginBridge bridge = FindFirstObjectByType<UnityPluginBridge>();
        if (bridge == null)
        {
            GameObject bridgeObj = new GameObject("UnityPluginBridge");
            bridge = bridgeObj.AddComponent<UnityPluginBridge>();
            if (enableDebugLogs)
                Debug.Log("SceneSetupHelper: Created UnityPluginBridge");
        }
    }

    private void SetupMapRenderer()
    {
        MapAccessorRendererSafe mapRenderer = FindFirstObjectByType<MapAccessorRendererSafe>();
        if (mapRenderer == null)
        {
            // Check if there's an existing MapAccessorRenderer GameObject to replace
            GameObject mapObj = GameObject.Find("MapAccessorRenderer");
            if (mapObj == null)
            {
                mapObj = new GameObject("MapAccessorRenderer");
            }

            mapRenderer = mapObj.AddComponent<MapAccessorRendererSafe>();
            mapRenderer.mapFilePath = mapFilePath;

            if (enableDebugLogs)
                Debug.Log($"SceneSetupHelper: Created MapAccessorRendererSafe with map: {mapFilePath}");
        }
    }

    private void SetupTrafficManager()
    {
        TrafficManagerSafe trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
        if (trafficManager == null)
        {
            // Check if there's an existing TrafficSimulationManager GameObject to replace
            GameObject trafficObj = GameObject.Find("TrafficSimulationManager");
            if (trafficObj == null)
            {
                trafficObj = new GameObject("TrafficSimulationManager");
            }

            trafficManager = trafficObj.AddComponent<TrafficManagerSafe>();
            trafficManager.numberOfAgents = numberOfAgents;
            trafficManager.randomSeed = randomSeed;

            // Create or find agent parent
            GameObject agentParent = GameObject.Find("TrafficAgents");
            if (agentParent == null)
            {
                agentParent = new GameObject("TrafficAgents");
                agentParent.transform.SetParent(trafficObj.transform);
            }
            trafficManager.agentParent = agentParent.transform;

            if (enableDebugLogs)
                Debug.Log($"SceneSetupHelper: Created TrafficManagerSafe with {numberOfAgents} agents");
        }
    }

    private void FixCameraReferences()
    {
        // Find DynamicBEVCameraController and help it find the new agent structure
        DynamicBEVCameraController cameraController = FindFirstObjectByType<DynamicBEVCameraController>();
        if (cameraController != null)
        {
            if (enableDebugLogs)
                Debug.Log("SceneSetupHelper: Found DynamicBEVCameraController, helping it find new traffic structure");

            // The camera will automatically find agents when they're created by TrafficManagerSafe
            // We just need to make sure it's looking in the right place
        }
    }

    [ContextMenu("Log Scene Status")]
    public void LogSceneStatus()
    {
        Debug.Log("=== SCENE STATUS ===");

        UnityPluginBridge bridge = FindFirstObjectByType<UnityPluginBridge>();
        Debug.Log($"UnityPluginBridge: {(bridge != null ? "✓ Present" : "✗ Missing")}");

        MapAccessorRendererSafe mapRenderer = FindFirstObjectByType<MapAccessorRendererSafe>();
        if (mapRenderer != null)
        {
            Debug.Log($"MapRenderer: ✓ Present, Loaded: {mapRenderer.IsMapLoaded()}");
        }
        else
        {
            Debug.Log("MapRenderer: ✗ Missing");
        }

        TrafficManagerSafe trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
        if (trafficManager != null)
        {
            Debug.Log($"TrafficManager: ✓ Present, Initialized: {trafficManager.IsTrafficInitialized()}");
        }
        else
        {
            Debug.Log("TrafficManager: ✗ Missing");
        }

        TrafficAgentSafe[] agents = FindObjectsByType<TrafficAgentSafe>(FindObjectsSortMode.None);
        Debug.Log($"Active Agents: {agents.Length}");

        DynamicBEVCameraController cameraController = FindFirstObjectByType<DynamicBEVCameraController>();
        Debug.Log($"Camera Controller: {(cameraController != null ? "✓ Present" : "✗ Missing")}");

        Debug.Log("=== END STATUS ===");
    }

    [ContextMenu("Create Test Traffic")]
    public void CreateTestTraffic()
    {
        Debug.Log("SceneSetupHelper: Creating test traffic for camera");

        TrafficManagerSafe trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
        if (trafficManager == null)
        {
            SetupTrafficManager();
            trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
        }

        if (trafficManager != null && !trafficManager.IsTrafficInitialized())
        {
            // Force restart traffic simulation to create agents
            trafficManager.RestartTrafficSimulation();
            Debug.Log("SceneSetupHelper: Traffic simulation restarted");
        }
    }

    [ContextMenu("Clean Old Components")]
    public void CleanOldComponents()
    {
        Debug.Log("SceneSetupHelper: Cleaning up old component references");

        // Find GameObjects with missing script components and log them
        MonoBehaviour[] allComponents = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int missingCount = 0;

        foreach (var component in allComponents)
        {
            if (component == null)
            {
                missingCount++;
            }
        }

        Debug.Log($"SceneSetupHelper: Found {missingCount} missing script references (these are expected from disabled P/Invoke scripts)");
    }
}
using UnityEngine;
using System.Collections;

// Bootstrap script that automatically runs when the scene loads to set up P/Invoke-free system
// This should be attached to any GameObject in the scene or will create itself
[DefaultExecutionOrder(-200)] // Run before everything else
public class SceneBootstrapper : MonoBehaviour
{
    private static bool hasBootstrapped = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnSceneLoaded()
    {
        if (hasBootstrapped) return;
        hasBootstrapped = true;

        Debug.Log("SceneBootstrapper: Auto-creating P/Invoke-free system");

        // Create a bootstrapper GameObject if none exists
        SceneBootstrapper bootstrapper = FindFirstObjectByType<SceneBootstrapper>();
        if (bootstrapper == null)
        {
            GameObject bootstrapperObj = new GameObject("SceneBootstrapper");
            bootstrapper = bootstrapperObj.AddComponent<SceneBootstrapper>();
        }

        // Start the bootstrap process
        bootstrapper.StartCoroutine(bootstrapper.BootstrapScene());
    }

    private IEnumerator BootstrapScene()
    {
        Debug.Log("SceneBootstrapper: Starting P/Invoke-free system bootstrap");

        // Wait a frame to ensure all GameObjects are loaded
        yield return null;

        // Step 1: Create or find UnityPluginBridge
        yield return SetupPluginBridge();

        // Step 2: Setup MapAccessorRendererSafe on existing MapAccessorRenderer GameObject
        yield return SetupMapRenderer();

        // Step 3: Setup TrafficManagerSafe on existing TrafficSimulationManager GameObject
        yield return SetupTrafficManager();

        // Step 4: Create agents (this will make the camera find them)
        yield return CreateTrafficAgents();

        Debug.Log("SceneBootstrapper: P/Invoke-free system bootstrap complete!");

        // Log final status
        LogSystemStatus();
    }

    private IEnumerator SetupPluginBridge()
    {
        UnityPluginBridge bridge = FindFirstObjectByType<UnityPluginBridge>();
        if (bridge == null)
        {
            GameObject bridgeObj = new GameObject("UnityPluginBridge");
            bridge = bridgeObj.AddComponent<UnityPluginBridge>();
            Debug.Log("SceneBootstrapper: Created UnityPluginBridge");
        }
        else
        {
            Debug.Log("SceneBootstrapper: UnityPluginBridge already exists");
        }
        yield return null;
    }

    private IEnumerator SetupMapRenderer()
    {
        // Find the existing MapAccessorRenderer GameObject
        GameObject mapObj = GameObject.Find("MapAccessorRenderer");
        if (mapObj != null)
        {
            // Add our safe component to the existing GameObject
            MapAccessorRendererSafe mapRenderer = mapObj.GetComponent<MapAccessorRendererSafe>();
            if (mapRenderer == null)
            {
                mapRenderer = mapObj.AddComponent<MapAccessorRendererSafe>();
                mapRenderer.mapFilePath = "Assets/Maps/data.xodr";
                Debug.Log("SceneBootstrapper: Added MapAccessorRendererSafe to existing MapAccessorRenderer GameObject");
            }
        }
        else
        {
            Debug.LogWarning("SceneBootstrapper: MapAccessorRenderer GameObject not found in scene");
        }
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator SetupTrafficManager()
    {
        // Find the existing TrafficSimulationManager GameObject (this is what the camera is looking for)
        GameObject trafficObj = GameObject.Find("TrafficSimulationManager");
        if (trafficObj != null)
        {
            // Add our safe component to the existing GameObject
            TrafficManagerSafe trafficManager = trafficObj.GetComponent<TrafficManagerSafe>();
            if (trafficManager == null)
            {
                trafficManager = trafficObj.AddComponent<TrafficManagerSafe>();
                trafficManager.numberOfAgents = 3;
                trafficManager.randomSeed = 12345;

                // Set agents to be direct children of TrafficSimulationManager
                // This allows the camera to find agent_0
                trafficManager.agentParent = trafficObj.transform;

                Debug.Log("SceneBootstrapper: Added TrafficManagerSafe to existing TrafficSimulationManager GameObject");
            }
        }
        else
        {
            Debug.LogWarning("SceneBootstrapper: TrafficSimulationManager GameObject not found in scene");
        }
        yield return new WaitForSeconds(0.1f);
    }

    private IEnumerator CreateTrafficAgents()
    {
        // Wait a bit more for the traffic system to initialize
        yield return new WaitForSeconds(0.5f);

        // Check if agents were created
        TrafficAgentSafe[] agents = FindObjectsByType<TrafficAgentSafe>(FindObjectsSortMode.None);
        Debug.Log($"SceneBootstrapper: Found {agents.Length} traffic agents");

        if (agents.Length == 0)
        {
            // If no agents were created automatically, try to restart the traffic system
            TrafficManagerSafe trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
            if (trafficManager != null)
            {
                Debug.Log("SceneBootstrapper: No agents found, restarting traffic simulation");
                trafficManager.RestartTrafficSimulation();
                yield return new WaitForSeconds(0.5f);

                // Check again
                agents = FindObjectsByType<TrafficAgentSafe>(FindObjectsSortMode.None);
                Debug.Log($"SceneBootstrapper: After restart, found {agents.Length} traffic agents");
            }
        }

        // Rename first agent to match camera expectations
        if (agents.Length > 0)
        {
            GameObject firstAgent = agents[0].gameObject;
            if (firstAgent.name != "agent_0")
            {
                firstAgent.name = "agent_0";
                Debug.Log("SceneBootstrapper: Renamed first agent to 'agent_0' for camera compatibility");
            }
        }
    }

    private void LogSystemStatus()
    {
        Debug.Log("=== SCENE BOOTSTRAP STATUS ===");

        UnityPluginBridge bridge = FindFirstObjectByType<UnityPluginBridge>();
        Debug.Log($"UnityPluginBridge: {(bridge != null ? "✓" : "✗")}");

        MapAccessorRendererSafe mapRenderer = FindFirstObjectByType<MapAccessorRendererSafe>();
        Debug.Log($"MapAccessorRendererSafe: {(mapRenderer != null ? "✓" : "✗")}");

        TrafficManagerSafe trafficManager = FindFirstObjectByType<TrafficManagerSafe>();
        Debug.Log($"TrafficManagerSafe: {(trafficManager != null ? "✓" : "✗")}");

        TrafficAgentSafe[] agents = FindObjectsByType<TrafficAgentSafe>(FindObjectsSortMode.None);
        Debug.Log($"TrafficAgentSafe count: {agents.Length}");

        // Check if camera target exists now
        GameObject agent0 = GameObject.Find("agent_0");
        Debug.Log($"agent_0 GameObject: {(agent0 != null ? "✓" : "✗")}");

        GameObject trafficSimManager = GameObject.Find("TrafficSimulationManager");
        if (trafficSimManager != null && agent0 != null)
        {
            Debug.Log("Camera should now be able to find 'agent_0' in 'TrafficSimulationManager'");
        }

        Debug.Log("=== END STATUS ===");
    }

    // Context menu for manual testing
    [ContextMenu("Bootstrap Now")]
    public void BootstrapNow()
    {
        if (Application.isPlaying)
        {
            StartCoroutine(BootstrapScene());
        }
        else
        {
            Debug.LogWarning("SceneBootstrapper: Cannot bootstrap in edit mode");
        }
    }
}
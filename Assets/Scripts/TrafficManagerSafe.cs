using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// P/Invoke-free TrafficManager that uses UnityPluginBridge
public class TrafficManagerSafe : MonoBehaviour
{
    [Header("Traffic Simulation Settings")]
    public int numberOfAgents = 1;
    public uint randomSeed = 12345;
    public float timeStep = 0.1f;
    public float maxVehicleSpeed = 60.0f;

    [Header("Agent Settings")]
    public GameObject agentPrefab;
    public Transform agentParent;
    [Range(0.1f, 2.0f)]
    public float vehicleScale = 0.25f; // Scale factor for vehicle size (0.25 = 25% of original)

    private IntPtr trafficSimulation = IntPtr.Zero;
    private UnityPluginBridge pluginBridge;
    private MapAccessorRendererSafe mapRenderer;
    private List<GameObject> agentObjects = new List<GameObject>();
    private bool isTrafficInitialized = false;

    void Start()
    {
        Debug.Log("TrafficManagerSafe: Starting P/Invoke-free traffic simulation");

        // Auto-load NISSAN-GTR prefab if not assigned
        if (agentPrefab == null)
        {
            LoadNissanGTRPrefab();
        }

        // Find required components
        pluginBridge = FindFirstObjectByType<UnityPluginBridge>();
        if (pluginBridge == null)
        {
            Debug.LogError("TrafficManagerSafe: UnityPluginBridge not found!");
            return;
        }

        mapRenderer = FindFirstObjectByType<MapAccessorRendererSafe>();
        if (mapRenderer == null)
        {
            Debug.LogWarning("TrafficManagerSafe: MapAccessorRendererSafe not found - traffic will spawn without map");
        }

        // Wait for systems to initialize then create traffic simulation
        StartCoroutine(InitializeTrafficSimulation());
    }

    private void LoadNissanGTRPrefab()
    {
#if UNITY_EDITOR
        // In editor, load directly from asset path
        agentPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NISSAN-GTR.prefab");
        if (agentPrefab != null)
        {
            Debug.Log("TrafficManagerSafe: Loaded NISSAN-GTR prefab from Assets/Prefabs/");
            return;
        }
#endif

        // Try loading from Resources folder
        agentPrefab = Resources.Load<GameObject>("Prefabs/NISSAN-GTR");
        if (agentPrefab != null)
        {
            Debug.Log("TrafficManagerSafe: Loaded NISSAN-GTR prefab from Resources/Prefabs/");
            return;
        }

        agentPrefab = Resources.Load<GameObject>("NISSAN-GTR");
        if (agentPrefab != null)
        {
            Debug.Log("TrafficManagerSafe: Loaded NISSAN-GTR prefab from Resources/");
            return;
        }

        Debug.LogWarning("TrafficManagerSafe: Could not find NISSAN-GTR prefab, will use default cube");
    }

    private IEnumerator InitializeTrafficSimulation()
    {
        // Wait for map to load if available
        if (mapRenderer != null)
        {
            Debug.Log("TrafficManagerSafe: Waiting for map to load...");
            float timeout = 5.0f;
            while (!mapRenderer.IsMapLoaded() && timeout > 0)
            {
                yield return new WaitForSeconds(0.1f);
                timeout -= 0.1f;
            }

            if (timeout <= 0)
            {
                Debug.LogWarning("TrafficManagerSafe: Timeout waiting for map - proceeding without map");
            }
            else
            {
                Debug.Log("TrafficManagerSafe: Map loaded successfully");
            }
        }

        // Create traffic simulation
        CreateTrafficSimulation();
    }

    private void CreateTrafficSimulation()
    {
        try
        {
            Debug.Log($"TrafficManagerSafe: Creating traffic simulation with {numberOfAgents} agents");

            // Create the traffic simulation
            trafficSimulation = pluginBridge.CreateTrafficSimulation(numberOfAgents, randomSeed);

            if (trafficSimulation == IntPtr.Zero)
            {
                Debug.LogError("TrafficManagerSafe: Failed to create traffic simulation");
                return;
            }

            Debug.Log($"TrafficManagerSafe: Traffic simulation created: {pluginBridge.GetHandleInfo(trafficSimulation)}");

            // Assign map if available
            if (mapRenderer != null && mapRenderer.IsMapLoaded())
            {
                IntPtr mapAccessor = mapRenderer.GetMapAccessor();
                if (mapAccessor != IntPtr.Zero)
                {
                    pluginBridge.AssignMapToTraffic(trafficSimulation, mapAccessor);
                    Debug.Log("TrafficManagerSafe: Map assigned to traffic simulation");
                }
            }

            // Initialize agents
            pluginBridge.InitializeTrafficAgents(trafficSimulation);
            Debug.Log("TrafficManagerSafe: Traffic agents initialized");

            // Create Unity GameObjects for agents
            CreateAgentGameObjects();

            isTrafficInitialized = true;
            Debug.Log("TrafficManagerSafe: Traffic simulation setup complete!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"TrafficManagerSafe: Exception creating traffic simulation: {e.Message}");
        }
    }

    private void CreateAgentGameObjects()
    {
        Debug.Log($"TrafficManagerSafe: Creating {numberOfAgents} agent GameObjects");

        // Clear existing agents
        ClearAgentGameObjects();

        // Create parent object if not assigned
        if (agentParent == null)
        {
            GameObject parentObj = GameObject.Find("TrafficAgents");
            if (parentObj == null)
            {
                parentObj = new GameObject("TrafficAgents");
            }
            agentParent = parentObj.transform;
        }

        // Create agent GameObjects
        for (int i = 0; i < numberOfAgents; i++)
        {
            GameObject agentObj = CreateAgentObject(i);
            if (agentObj != null)
            {
                agentObjects.Add(agentObj);
            }
        }

        Debug.Log($"TrafficManagerSafe: Created {agentObjects.Count} agent GameObjects");
    }

    private GameObject CreateAgentObject(int agentId)
    {
        try
        {
            GameObject agentObj;

            if (agentPrefab != null)
            {
                agentObj = Instantiate(agentPrefab, agentParent);

                // Scale down the vehicle to match road proportions (observed vehicles are ~3-4x too large)
                agentObj.transform.localScale = Vector3.one * vehicleScale;

                // Apply a unique color tint to differentiate vehicles
                Renderer[] renderers = agentObj.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        // Create instance of material to avoid modifying the prefab
                        Material instanceMat = new Material(renderer.material);
                        float hue = (float)agentId / numberOfAgents;
                        Color tintColor = Color.HSVToRGB(hue, 0.5f, 1f);
                        instanceMat.color = Color.Lerp(instanceMat.color, tintColor, 0.3f);
                        renderer.material = instanceMat;
                    }
                }
            }
            else
            {
                // Create simple default agent
                agentObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                agentObj.transform.SetParent(agentParent);
                agentObj.transform.localScale = new Vector3(2f, 1f, 5f); // Car-like dimensions

                // Add basic material
                Renderer renderer = agentObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material agentMaterial = new Material(Shader.Find("Standard"));
                    agentMaterial.color = Color.Lerp(Color.red, Color.blue, (float)agentId / numberOfAgents);
                    renderer.material = agentMaterial;
                }
            }

            agentObj.name = $"Agent_{agentId}";

            // Get the actual simulation position for this agent
            Vector3[] simPositions = pluginBridge.GetAgentPositions(trafficSimulation);
            Vector3 initialPos = Vector3.zero;
            if (agentId < simPositions.Length)
            {
                initialPos = simPositions[agentId];
                // Adjust height based on whether using vehicle prefab or cube
                if (agentPrefab != null)
                {
                    initialPos.y = 0f; // Vehicle should be at ground level
                }
                else
                {
                    initialPos.y = 1f; // Cube needs to be raised
                }
            }
            else
            {
                // Fallback if simulation not ready
                initialPos = new Vector3(0f, agentPrefab != null ? 0f : 1f, 0f);
            }
            agentObj.transform.position = initialPos;

            // IMPORTANT: Add TrafficAgentSafe FIRST before ML-Agents components
            // This ensures VehicleMLAgent can find TrafficAgentSafe during Initialize()
            TrafficAgentSafe agentComponent = agentObj.GetComponent<TrafficAgentSafe>();
            if (agentComponent == null)
            {
                agentComponent = agentObj.AddComponent<TrafficAgentSafe>();
            }
            agentComponent.Initialize(agentId, this);

            // NOW configure ML-Agents components after TrafficAgentSafe is added
            var mlAgent = agentObj.GetComponent<Unity.MLAgents.Agent>();
            if (mlAgent != null)
            {
                // Keep ML-Agent enabled but ensure it works with our traffic system
                mlAgent.enabled = true;
                Debug.Log($"TrafficManagerSafe: Found existing ML-Agent component on agent {agentId}: {mlAgent.GetType().Name}");

                // Ensure VehicleMLAgent is present if this is a VehicleMLAgent
                var vehicleMLAgent = agentObj.GetComponent<VehicleMLAgent>();
                if (vehicleMLAgent != null)
                {
                    // Set the TrafficAgentSafe reference on VehicleMLAgent
                    vehicleMLAgent.SetTrafficAgent(agentComponent);
                    Debug.Log($"TrafficManagerSafe: Set TrafficAgentSafe on VehicleMLAgent for agent {agentId}");
                }
                else
                {
                    Debug.LogWarning($"TrafficManagerSafe: ML-Agent found but it's not VehicleMLAgent type on agent {agentId}");
                }
            }
            else
            {
                // Add our VehicleMLAgent component if no ML-Agent exists
                var vehicleMLAgent = agentObj.AddComponent<VehicleMLAgent>();
                vehicleMLAgent.SetTrafficAgent(agentComponent);
                Debug.Log($"TrafficManagerSafe: Added VehicleMLAgent component to agent {agentId}");
            }

            // Initialize ML-Agents after all components are in place
            agentComponent.InitializeMLAgents();

            Debug.Log($"TrafficManagerSafe: Created agent {agentId} at position {initialPos}");
            return agentObj;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"TrafficManagerSafe: Exception creating agent {agentId}: {e.Message}");
            return null;
        }
    }

    private void ClearAgentGameObjects()
    {
        foreach (GameObject agentObj in agentObjects)
        {
            if (agentObj != null)
            {
                DestroyImmediate(agentObj);
            }
        }
        agentObjects.Clear();
    }

    void Update()
    {
        if (isTrafficInitialized && trafficSimulation != IntPtr.Zero)
        {
            // Update traffic simulation (placeholder for now)
            UpdateTrafficSimulation();
        }
    }

    private void UpdateTrafficSimulation()
    {
        // Collect actions from ML-Agents
        int[] highLevelActions = new int[numberOfAgents];
        float[][] lowLevelActions = new float[numberOfAgents][];

        bool hasMLActions = false;

        for (int i = 0; i < agentObjects.Count; i++)
        {
            if (agentObjects[i] != null)
            {
                VehicleMLAgent mlAgent = agentObjects[i].GetComponent<VehicleMLAgent>();
                if (mlAgent != null)
                {
                    highLevelActions[i] = mlAgent.GetHighLevelAction();
                    lowLevelActions[i] = mlAgent.GetLowLevelActions();
                    hasMLActions = true;
                }
                else
                {
                    // Default actions if no ML-Agent
                    highLevelActions[i] = 0; // Maintain lane
                    lowLevelActions[i] = new float[] { 0f, 0.5f, 0f }; // Straight, moderate speed, no braking
                }
            }
        }

        // Step the traffic simulation logic with ML-Agents actions
        if (hasMLActions)
        {
            pluginBridge.StepTrafficSimulation(trafficSimulation, Time.deltaTime, highLevelActions, lowLevelActions);
        }
        else
        {
            // Fallback to original method if no ML actions
            pluginBridge.StepTrafficSimulation(trafficSimulation, Time.deltaTime);
        }

        // Get updated agent states from simulation
        Vector3[] agentPositions = pluginBridge.GetAgentPositions(trafficSimulation);
        Vector3[] agentVelocities = pluginBridge.GetAgentVelocities(trafficSimulation);
        float[] agentYaws = pluginBridge.GetAgentYaws(trafficSimulation);

        // Debug: Log simulation data every 60 frames
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"TrafficManagerSafe: Simulation stepped - {agentPositions.Length} agents, deltaTime: {Time.deltaTime:F3}");
            if (agentPositions.Length > 0)
            {
                Debug.Log($"TrafficManagerSafe: Agent 0 - Pos: {agentPositions[0]}, Vel: {agentVelocities[0]}, Yaw: {agentYaws[0]:F1}°");
            }
        }

        // Update Unity GameObjects with simulation state
        for (int i = 0; i < agentObjects.Count && i < agentPositions.Length; i++)
        {
            if (agentObjects[i] != null)
            {
                TrafficAgentSafe agentComponent = agentObjects[i].GetComponent<TrafficAgentSafe>();
                if (agentComponent != null)
                {
                    // Set agent state from C++ simulation
                    agentComponent.SetSimulationState(
                        agentPositions[i],
                        agentVelocities[i],
                        agentYaws[i]
                    );

                    // Call UpdateAgent to process the simulation state
                    agentComponent.UpdateAgent(Time.deltaTime);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (trafficSimulation != IntPtr.Zero && pluginBridge != null)
        {
            Debug.Log("TrafficManagerSafe: Cleaning up traffic simulation");
            ClearAgentGameObjects();
            pluginBridge.DestroyTrafficSimulation(trafficSimulation);
            trafficSimulation = IntPtr.Zero;
        }
    }

    // Public methods for agent access
    public IntPtr GetTrafficSimulation()
    {
        return trafficSimulation;
    }

    public UnityPluginBridge GetPluginBridge()
    {
        return pluginBridge;
    }

    public bool IsTrafficInitialized()
    {
        return isTrafficInitialized;
    }

    // Debug methods
    public void LogTrafficInfo()
    {
        if (pluginBridge != null && trafficSimulation != IntPtr.Zero)
        {
            Debug.Log($"TrafficManagerSafe: {pluginBridge.GetHandleInfo(trafficSimulation)}");
        }
        else
        {
            Debug.Log("TrafficManagerSafe: No traffic simulation active");
        }

        Debug.Log($"TrafficManagerSafe: Active agents: {agentObjects.Count}");
    }

    public void RestartTrafficSimulation()
    {
        Debug.Log("TrafficManagerSafe: Restarting traffic simulation");

        if (trafficSimulation != IntPtr.Zero && pluginBridge != null)
        {
            ClearAgentGameObjects();
            pluginBridge.DestroyTrafficSimulation(trafficSimulation);
            trafficSimulation = IntPtr.Zero;
        }

        isTrafficInitialized = false;
        StartCoroutine(InitializeTrafficSimulation());
    }

    // Callback for when native library is ready
    private void OnNativeLibraryReady()
    {
        Debug.Log("TrafficManagerSafe: Received native library ready callback");
        if (!isTrafficInitialized)
        {
            StartCoroutine(InitializeTrafficSimulation());
        }
    }
}
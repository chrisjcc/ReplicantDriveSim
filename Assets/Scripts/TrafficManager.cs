using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


// Responsible for stepping the traffic simulation and updating all agents
public class TrafficManager : MonoBehaviour
{
    // Constants
    private const string DllName = "ReplicantDriveSim";

    // DllImport Declarations
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FloatVector_size(IntPtr vector);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float FloatVector_get(IntPtr vector, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FloatVector_destroy(IntPtr vector);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int StringFloatVectorMap_size(IntPtr map);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr StringFloatVectorMap_get_key(IntPtr map, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr StringFloatVectorMap_get_value(IntPtr map, IntPtr key);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void StringFloatVectorMap_destroy(IntPtr map);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_create(int num_agents, uint seed);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Traffic_destroy(IntPtr traffic);

    //[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    //public static extern void Traffic_step(IntPtr traffic, int[] high_level_actions, float[] low_level_actions);
    //public static extern void Traffic_step(IntPtr traffic, int[] high_level_actions, LowLevelAction low_level_actions);
    //public static extern void Traffic_step(IntPtr traffic,
    //                                       [In, MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] int[] high_level_actions,
    //                                       [In, MarshalAs(UnmanagedType.LPArray, SizeConst = 3)] float[] low_level_actions);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Traffic_step(IntPtr traffic);
    //public static extern void Traffic_step(IntPtr traffic, int[] high_level_actions, float[][] low_level_actions);
    //public static extern void Traffic_step(IntPtr traffic, int[] high_level_action);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_get_agent_positions(IntPtr traffic);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_get_agent_velocities(IntPtr traffic);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_get_agent_orientations(IntPtr traffic);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_get_previous_positions(IntPtr traffic);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_get_agents(IntPtr traffic);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vehicle_setX(IntPtr vehicle, float x);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float Vehicle_getX(IntPtr vehicle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vehicle_setY(IntPtr vehicle, float y);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float Vehicle_getY(IntPtr vehicle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vehicle_setZ(IntPtr vehicle, float z);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float Vehicle_getZ(IntPtr vehicle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr VehiclePtrVector_get(IntPtr vector, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int VehiclePtrVector_size(IntPtr vector);

    // Public Fields
    public IntPtr trafficSimulationPtr;
    public IntPtr agentPositionsMap;
    public IntPtr agentVelocitiesMap;
    public IntPtr agentOrientationsMap;
    public IntPtr agentPreviousPositionsMap;

    public float moveSpeed = 5f;
    public float rotationSpeed = 100f;

    [SerializeField]
    public GameObject agentPrefab; // Reference to the agent prefab (e.g., a car model such as Mercedes-Benz AMG GT-R)

    [SerializeField]
    public int initialAgentCount = 2;

    [SerializeField]
    public uint seed = 42;

    [SerializeField]
    public int numberOfRays = 15;

    [SerializeField]
    public float rayLength = 200f;

    [SerializeField]
    public float raycastAngle = 90f;

    [SerializeField]
    public bool debugVisualization = false;

    // Non-serialized Fields
    public float spawnAreaSize = 100f;
    public float spawnHeight = 0.5f;

    public Dictionary<string, TrafficAgent> agentInstances = new Dictionary<string, TrafficAgent>();
    public Dictionary<string, Collider> agentColliders = new Dictionary<string, Collider>();

    void Awake()
    {
        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::Awake START ===");
        #endif

        if (agentPrefab == null)
        {
            Debug.LogError("Agent prefab is not assigned. Please assign it in the inspector.");
            enabled = false;
            return;
        }

        // Validate that the prefab has necessary components
        if (!agentPrefab.GetComponent<TrafficAgent>())
        {
            Debug.LogError("Agent prefab does not have a TrafficAgent component. Please add one.");
            enabled = false;
            return;
        }
        Debug.Log("=== TrafficManager::Awake END ===");
    }

    void Start()
    {
        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::Start START ===");
        #endif

        // Assuming you have a reference to the Traffic_create and Traffic_destroy functions from the C API
        trafficSimulationPtr = Traffic_create(initialAgentCount, seed); // Create simulation with 2 agents and seed 12345

        if (trafficSimulationPtr == IntPtr.Zero)
        {
            Debug.LogError("Failed to create traffic simulation.");
            enabled = false;
            return;
        }

        // Get the initial state of agent
        agentPositionsMap = Traffic_get_agent_positions(trafficSimulationPtr);
        agentVelocitiesMap = Traffic_get_agent_velocities(trafficSimulationPtr);
        agentOrientationsMap = Traffic_get_agent_orientations(trafficSimulationPtr);
        agentPreviousPositionsMap = Traffic_get_previous_positions(trafficSimulationPtr);

        InitializeAgents();

        Debug.Log("=== TrafficManager::Start END ===");
    }

    public void InitializeAgents()
    {
        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::InitializeAgents START ===");
        #endif

        for (int i = 0; i < initialAgentCount; i++)
        {
            try
            {
                SpawnAgent(i);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to spawn agent {i}: {e.Message}");
            }
        }
        #if UNITY_EDITOR
        Debug.Log($"Number of agents: {agentInstances.Count}");
        Debug.Log($"Number of collider boxes: {agentColliders.Count}");
        #endif

        Debug.Log("=== TrafficManager::InitializeAgents END ===");
    }

    void SpawnAgent(int i)
    {
        IntPtr agentIdPtr = StringFloatVectorMap_get_key(agentPositionsMap, i);

        if (agentIdPtr == IntPtr.Zero)
        {
            throw new Exception($"Failed to get agent ID for index {i}");
        }

        string agentId = Marshal.PtrToStringAnsi(agentIdPtr);

        IntPtr positionPtr = StringFloatVectorMap_get_value(agentPositionsMap, agentIdPtr);
        IntPtr orientationPtr = StringFloatVectorMap_get_value(agentOrientationsMap, agentIdPtr);

        if (positionPtr == IntPtr.Zero || orientationPtr == IntPtr.Zero)
        {
            throw new Exception($"Failed to get position or orientation for agent {agentId}");
        }

        Vector3 position = GetVector3FromFloatVector(positionPtr);
        Quaternion rotation = GetQuaternionFromFloatVector(orientationPtr);

        if (agentInstances.ContainsKey(agentId))
        {
            // Agent already exists, update its position and rotation
            Debug.Log($"Agent ID: {agentId} already exists. Updating position and rotation.");
            TrafficAgent existingAgent = agentInstances[agentId];
            existingAgent.transform.position = position;
            existingAgent.transform.rotation = rotation;
            Debug.Log($"Updated agent: {agentId} to position: {position}");

            // Ensure the collider is updated if it's not part of the main GameObject
            if (agentColliders.TryGetValue(agentId, out Collider existingCollider))
            {
                if (existingCollider.transform != agentInstances[agentId].transform)
                {
                    existingCollider.transform.position = position;
                    existingCollider.transform.rotation = rotation;
                }
            }
            else
            {
                Debug.LogWarning($"Collider not found for existing agent {agentId}. Attempting to find or add one.");
                UpdateColliderForExistingAgent(existingAgent.gameObject, agentId);
            }
        }
        else
        {
            // Create new agent GameObject with the specified position and orientation
            GameObject agentObject = Instantiate(agentPrefab, position, rotation);

            agentObject.name = agentId; // Set the name of the instantiated object to the agent ID
            agentObject.transform.SetParent(this.transform); // Use 'this.transform' to refer to the TrafficSimulationManager's transform
            Debug.Log($"Instantiated agent: {agentId}, Prefab name: {agentPrefab.name}");

            TrafficAgent agent = agentObject.GetComponent<TrafficAgent>();

            if (agent == null)
            {
                Debug.LogWarning($"TrafficAgent component not found on the instantiated prefab for {agentId}. Adding it manually.");
                agent = agentObject.AddComponent<TrafficAgent>();
            }

            agent.Initialize();
            agentInstances[agentId] = agent;
            //agentInstances.Add(agentId, agent);

            UpdateColliderForExistingAgent(agentObject, agentId);
        }

        // Clean up the resources
        /*
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentPositionsMap);
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentVelocitiesMap);
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentVelocitiesMap);
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentOrientationsMap);
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentPreviousPositionsMap);
        */
    }

    private Vector3 GetVector3FromFloatVector(IntPtr vectorPtr)
    {
        if (FloatVector_size(vectorPtr) < 3)
        {
            throw new Exception("FloatVector has insufficient size for Vector3");
        }
        return new Vector3(
            FloatVector_get(vectorPtr, 0),
            FloatVector_get(vectorPtr, 1),
            FloatVector_get(vectorPtr, 2)
        );
    }

    private Quaternion GetQuaternionFromFloatVector(IntPtr vectorPtr)
    {
        if (FloatVector_size(vectorPtr) < 3)
        {
            throw new Exception("FloatVector has insufficient size for Euler angles");
        }
        float roll  = FloatVector_get(vectorPtr, 0);
        float pitch = FloatVector_get(vectorPtr, 1);
        float yaw   = FloatVector_get(vectorPtr, 2);

        // Convert Euler angles to Quaternion, Euler angles (roll, pitch, yaw)
        return Quaternion.Euler(roll * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, pitch * Mathf.Rad2Deg);
    }

    private void UpdateColliderForExistingAgent(GameObject agentObject, string agentId)
    {
        // Get the collider component from the instantiated agent or its children recursively
        Collider agentCollider = FindColliderRecursively(agentObject);
        Debug.Log($"agentCollider: {(agentCollider != null ? agentCollider.GetType().Name : "Not found")}");

        if (agentCollider == null)
        {
            Debug.LogWarning($"No Collider found on the agent {agentId} or its children. Adding a BoxCollider.");
            agentCollider = agentObject.AddComponent<BoxCollider>();
        }

        agentColliders[agentId] = agentCollider;
        // agentColliders.Add(agentId, agentCollider);
        Debug.Log($"Updated collider for Agent ID: {agentId}");
    }

    // Helper method to find collider recursively
    private Collider FindColliderRecursively(GameObject obj)
    {
        #if UNITY_EDITOR
        Debug.Log($"Searching for Collider in: {obj.name}");
        #endif

        // Check if the object itself has a Collider
        Collider collider = obj.GetComponent<Collider>();

        if (collider != null)
        {
            Debug.Log($"Found Collider on: {obj.name}");
            Collider[] childColliders = obj.GetComponentsInChildren<Collider>(true);
            Debug.Log($"Found {childColliders.Length} Collider(s) in children of: {obj.name}");
            return collider;
        }

        foreach (Transform child in obj.transform)
        {
            collider = FindColliderRecursively(child.gameObject);
            if (collider != null) return collider;
        }

        #if UNITY_EDITOR
        Debug.Log($"No Collider found in: {obj.name} or its children");
        #endif

        return null;
    }

    // Helper method to get the full path of a GameObject in the hierarchy
    private string GetGameObjectPath(GameObject obj)
    {
        string path = obj.name;
        while (obj.transform.parent != null)
        {
            obj = obj.transform.parent.gameObject;
            path = obj.name + "/" + path;
        }
        return path;
    }

    ///private void Update()
    private void FixedUpdate()
    {
        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::FixedUpdate START ===");
        #endif

        //Debug.Log($"Time.timeScale: {Time.timeScale}");


        if (agentInstances == null || agentColliders == null)
        {
            Debug.LogError("Agent instances or colliders are null. Make sure they are properly initialized.");
            return;
        }

        if (trafficSimulationPtr == IntPtr.Zero)
        {
            Debug.LogError("trafficSimulationPtr is null or invalid");
            return;
        }

        // Prepare arrays for high-level and low-level actions
        List<int> highLevelActions = new List<int>();
        List<float[]> lowLevelActions = new List<float[]>();

        Debug.Log("***** BEFORE *****");

        // After stepping the simulation, log the positions and actions for each agent
        int indexValue = 0;  // Reset indexValue to loop through agents again if needed
        foreach (var kvp in agentInstances)
        {
            string agentId = kvp.Key;
            TrafficAgent agent = kvp.Value;

            if (agent != null)
            {
                IntPtr vehiclePtrVectorHandle = Traffic_get_agents(trafficSimulationPtr);
                IntPtr vehiclePtr = VehiclePtrVector_get(vehiclePtrVectorHandle, indexValue);
                Debug.Log($"Agent {agentId} Position: {agent.transform.position}, Rotation: {agent.transform.rotation.eulerAngles}");
                indexValue++;
            }
        }

        Debug.Log("***** UPDATE *****");

        // Additional logging or processing if needed
        foreach (var kvp in agentInstances)
        {
            string agentId = kvp.Key;
            TrafficAgent agent = kvp.Value;

            if (agent != null)
            {

                // If actions are still 0, try setting some default values for testing
                //agent.highLevelActions = 1;
                //agent.lowLevelActions[0] = 0.1f;  // Small steering angle
                //agent.lowLevelActions[1] = 1.0f;  // Some forward acceleration
                //agent.lowLevelActions[2] = 0.0f;  // no baking

                // Collect the high-level and low-level actions for each agent
                highLevelActions.Add(agent.highLevelActions);
                lowLevelActions.Add(agent.lowLevelActions);

                Debug.Log($"Agent {agentId} - High-level actions: {string.Join(", ", agent.highLevelActions)}");
                Debug.Log($"Agent {agentId} - Low-level actions: {string.Join(", ", agent.lowLevelActions)}");

                Debug.Log($"Agent {agentId} Position: {agent.transform.position}, Rotation: {agent.transform.rotation.eulerAngles}");
            }
        }

        // Step the simulation once for all agents with the gathered actions
        //Traffic_step(trafficSimulationPtr, highLevelActions.ToArray(), lowLevelActions.ToArray());
        //Traffic_step(trafficSimulationPtr, highLevelActions.ToArray());
        //Traffic_step(trafficSimulationPtr);

        // Update agent positions based on the simulation step
        UpdateAgentPositions();
        Debug.Log("***** AFTER *****");

        indexValue = 0;

        foreach (var kvp in agentInstances)
        {
            string agentId = kvp.Key;
            TrafficAgent agent = kvp.Value;

            if (agent != null)
            {
                IntPtr vehiclePtrVectorHandle = Traffic_get_agents(trafficSimulationPtr);
                IntPtr vehiclePtr = VehiclePtrVector_get(vehiclePtrVectorHandle, indexValue);
                Debug.Log($"Agent {agentId} Position: {agent.transform.position}, Rotation: {agent.transform.rotation.eulerAngles}");
                indexValue++;
            }
        }

        Debug.Log("=== TrafficManager::FixedUpdate END ===");
    }

    // Update the positions of agents based on the simulation results
    public void UpdateAgentPositions()
    {
        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::UpdateAgentPositions START ===");
        #endif

        // Get the initial state of agent
        agentPositionsMap = Traffic_get_agent_positions(trafficSimulationPtr);
        agentVelocitiesMap = Traffic_get_agent_velocities(trafficSimulationPtr);
        agentOrientationsMap = Traffic_get_agent_orientations(trafficSimulationPtr);
        agentPreviousPositionsMap = Traffic_get_previous_positions(trafficSimulationPtr);

        // Process the agent positions
        for (int i = 0; i < initialAgentCount; i++)
        {
            IntPtr agentIdPtr = StringFloatVectorMap_get_key(agentPositionsMap, i);
            string agentId = Marshal.PtrToStringAnsi(agentIdPtr);

            IntPtr positionPtr = StringFloatVectorMap_get_value(agentPositionsMap, agentIdPtr);
            IntPtr orientationPtr = StringFloatVectorMap_get_value(agentOrientationsMap, agentIdPtr);

            if (positionPtr == IntPtr.Zero || orientationPtr == IntPtr.Zero)
            {
                throw new Exception($"Failed to get position or orientation for agent {agentId}");
            }

            Vector3 position = GetVector3FromFloatVector(positionPtr);
            Quaternion rotation = GetQuaternionFromFloatVector(orientationPtr);

            if (agentInstances.ContainsKey(agentId))
            {
                // Agent already exists, update its position and rotation
                Debug.Log($"Agent ID: {agentId} already exists. Updating position and rotation.");
                TrafficAgent existingAgent = agentInstances[agentId];
                existingAgent.transform.position = position;
                existingAgent.transform.rotation = rotation;
                Debug.Log($"Updated agent: {agentId} to position: {position} and rotation to: {rotation}");

                // Ensure the collider is updated if it's not part of the main GameObject
                if (agentColliders.TryGetValue(agentId, out Collider existingCollider))
                {
                    if (existingCollider.transform != agentInstances[agentId].transform)
                    {
                        existingCollider.transform.position = position;
                        existingCollider.transform.rotation = rotation;
                    }
                }
                else
                {
                    Debug.LogWarning($"Collider not found for existing agent {agentId}. Attempting to find or add one.");
                    UpdateColliderForExistingAgent(existingAgent.gameObject, agentId);
                }

            }
            else
            {
                // Create new agent GameObject with the specified position and orientation
                GameObject agentObject = Instantiate(agentPrefab, position, rotation);
                agentObject.name = agentId; // Set the name of the instantiated object to the agent ID
                agentObject.transform.SetParent(this.transform); // Use 'this.transform' to refer to the TrafficSimulationManager's transform
                Debug.Log($"Instantiated agent: {agentId}, Prefab name: {agentPrefab.name}");

                TrafficAgent agent = agentObject.GetComponent<TrafficAgent>();

                if (agent == null)
                {
                    Debug.LogWarning($"TrafficAgent component not found on the instantiated prefab for {agentId}. Adding it manually.");
                    agent = agentObject.AddComponent<TrafficAgent>();
                }

                agent.Initialize();
                agentInstances[agentId] = agent;
                //agentInstances.Add(agentId, agent);

                UpdateColliderForExistingAgent(agentObject, agentId);
            }

        }

        // Clean up the resources
        /*
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentPositionsMap);
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentVelocitiesMap);
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentVelocitiesMap);
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentOrientationsMap);
        TrafficManager.StringFloatVectorMap_destroy(trafficManager.agentPreviousPositionsMap);
        */
        Debug.Log("=== TrafficManager::UpdateAgentPositions END ===");
    }

    Vector3 GetRandomSpawnPosition()
    {
        float x = UnityEngine.Random.Range(-spawnAreaSize / 2, spawnAreaSize / 2);
        float z = UnityEngine.Random.Range(-spawnAreaSize / 2, spawnAreaSize / 2);

        return new Vector3(x, spawnHeight, z);
    }

    private void OnDestroy()
    {
        #if UNITY_EDITOR
        Debug.Log("-- TrafficManager::OnDestroy --");
        #endif

        if (trafficSimulationPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(trafficSimulationPtr);
            trafficSimulationPtr = IntPtr.Zero;
        }

        // Clean up agent instances
        if (agentInstances != null)
        {
            foreach (var agentInstance in agentInstances.Values)
            {
                if (agentInstance != null)
                {
                    // Remove DecisionRequester first
                    var decisionRequester = agentInstance.GetComponent<DecisionRequester>();
                    if (decisionRequester != null)
                    {
                        Destroy(decisionRequester);
                    }

                    // Then remove TrafficAgent
                    Destroy(agentInstance);
                }
            }
            agentInstances.Clear();
        }

        if (agentColliders != null)
        {
            agentColliders.Clear();
        }

        // Clean up map references
        CleanUpMap(ref agentPositionsMap);
        CleanUpMap(ref agentVelocitiesMap);
        CleanUpMap(ref agentOrientationsMap);
        CleanUpMap(ref agentPreviousPositionsMap);
    }

    private void CleanUpMap(ref IntPtr map)
    {
        if (map != IntPtr.Zero)
        {
            StringFloatVectorMap_destroy(map);
            map = IntPtr.Zero;
        }
    }
}

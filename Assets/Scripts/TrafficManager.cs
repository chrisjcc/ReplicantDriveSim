using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.SideChannels;


// Responsible for stepping the traffic simulation and updating all agents
public class TrafficManager : MonoBehaviour
{
    // Constants
    private const string DllName = "ReplicantDriveSim";

    // DllImport Declarations
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_create(int num_agents, uint seed);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Traffic_destroy(IntPtr traffic);

    // Note: Below SizeParamIndex is set 4, since it's the 4th parameter,
    // int low_level_actions_count, is the one that contains the size of the low_level_actions array.
    // This information is used by the .NET runtime to properly marshal the float[] array from the managed C# code to the unmanaged C++ code.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_step(IntPtr traffic,
                                         [In] int[] high_level_actions,
                                         int high_level_actions_count,
                                         [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] float[] low_level_actions,
                                         int low_level_actions_count);

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
    public static extern float Vehicle_getSteering(IntPtr vehicle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr VehiclePtrVector_get(IntPtr vector, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int VehiclePtrVector_size(IntPtr vector);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FreeString(IntPtr str);

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

    // Public Fields
    public IntPtr trafficSimulationPtr;
    public IntPtr agentPositionsMap;
    public IntPtr agentVelocitiesMap;
    public IntPtr agentOrientationsMap;
    public IntPtr agentPreviousPositionsMap;

    [HideInInspector]
    public float moveSpeed = 5f;

    [HideInInspector]
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
    [HideInInspector]
    public float spawnAreaSize = 100f;

    [HideInInspector]
    public float spawnHeight = 0.5f;

    public Dictionary<string, TrafficAgent> agentInstances = new Dictionary<string, TrafficAgent>();
    public Dictionary<string, Collider> agentColliders = new Dictionary<string, Collider>();

    List<int> highLevelActions;
    List<float[]> lowLevelActions;

    [HideInInspector]
    public float AngleStep;

    private FloatPropertiesChannel floatPropertiesChannel;

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

        AngleStep = raycastAngle / (numberOfRays - 1);

        highLevelActions = new List<int>();
        lowLevelActions = new List<float[]>();

        // Create the FloatPropertiesChannel
        floatPropertiesChannel = new FloatPropertiesChannel(new Guid("621f0a70-4f87-11ea-a6bf-784f4387d1f7"));

        // Register the channel
        SideChannelManager.RegisterSideChannel(floatPropertiesChannel);

        // Subscribe to the OnFloatPropertiesChanged event
        floatPropertiesChannel.RegisterCallback("initialAgentCount", OnInitialAgentCountChanged);

        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::Awake END ===");
        #endif
    }

    void Start()
    {
        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::Start START ===");
        #endif

        try
        {
            Debug.Log("Attempting to create traffic simulation");
            // Assuming you have a reference to the Traffic_create and Traffic_destroy functions from the C API
            trafficSimulationPtr = Traffic_create(initialAgentCount, seed); // Create simulation with 2 agents and seed 12345
            Debug.Log($"Traffic simulation created: {trafficSimulationPtr != IntPtr.Zero}");

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
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in TrafficManager Start: {e.Message}\n{e.StackTrace}");
        }

        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::Start END ===");
        #endif
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

        Debug.Log("=== TrafficManager::InitializeAgents END ===");
        #endif
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

        #if UNITY_EDITOR
        Debug.Log($"Agent {agentId}: Position = {position}, Rotation = {rotation.eulerAngles}");
        Debug.Log($"Euler angles: Pitch={rotation.eulerAngles.x}, Yaw={rotation.eulerAngles.y}, Roll={rotation.eulerAngles.z}");
        #endif

        if (agentInstances.ContainsKey(agentId))
        {
            // Agent already exists, update its position and rotation
            Debug.Log($"Agent ID: {agentId} already exists. Updating position and rotation.");
            //TrafficAgent existingAgent = agentInstances[agentId];
            var existingAgent = agentInstances[agentId]; // type TrafficAgent
            existingAgent.transform.position = position;
            existingAgent.transform.rotation = rotation;
            existingAgent.transform.hasChanged = true;
            Debug.Log($"Updated agent: {agentId} to position: {position} and rotation: {rotation}");

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
            agentInstances.Add(agentId, agent);

            UpdateColliderForExistingAgent(agentObject, agentId);
        }
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
        Quaternion rotation = Quaternion.Euler(
            roll * Mathf.Rad2Deg,  // X-axis rotation (roll)
            yaw * Mathf.Rad2Deg,   // Z-axis rotation (yaw)
            pitch * Mathf.Rad2Deg  // Y-axis rotation (pitch)
        );

        return rotation;
    }

    private void UpdateColliderForExistingAgent(GameObject agentObject, string agentId)
    {
        // Get the collider component from the instantiated agent or its children recursively
        Collider agentCollider = FindColliderRecursively(agentObject);

        #if UNITY_EDITOR
        Debug.Log($"agentCollider: {(agentCollider != null ? agentCollider.GetType().Name : "Not found")}");
        #endif

        if (agentCollider == null)
        {
            Debug.LogWarning($"No Collider found on the agent {agentId} or its children. Adding a BoxCollider.");
            agentCollider = agentObject.AddComponent<BoxCollider>();
        }

        agentColliders[agentId] = agentCollider;
        // agentColliders.Add(agentId, agentCollider);

        #if UNITY_EDITOR
        Debug.Log($"Updated collider for Agent ID: {agentId}");
        #endif
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

        // Additional logging or processing if needed (REMOVE LATER ON)
        foreach (var kvp in agentInstances)
        {
            string agentId = kvp.Key;
            var agent = kvp.Value; // type TrafficAgent

            if (agent != null)
            {
                // Collect the high-level and low-level actions for each agent
                highLevelActions.Add(agent.highLevelActions);
                lowLevelActions.Add(agent.lowLevelActions);
            }
        }

        // Step the simulation once for all agents with the gathered actions
        float[] flattenedLowLevelActions = lowLevelActions.SelectMany(a => a).ToArray();

        IntPtr resultPtr = Traffic_step(trafficSimulationPtr,
            highLevelActions.ToArray(),
            highLevelActions.Count,
            flattenedLowLevelActions,
            lowLevelActions.Count * 3 // Assuming each inner array has 3 elements (e.g., steering, throttle, braking)
            );

        if (resultPtr != IntPtr.Zero)
        {
            string result = Marshal.PtrToStringAnsi(resultPtr); // PtrToStringUTF8
            //Debug.Log($"Traffic_step result {resultPtr}:\n" + result);
            FreeString(resultPtr); // Don't forget to free the allocated memory
        }
        else
        {
            Debug.LogError("Traffic_step returned null pointer");
        }

        UpdateAgentPositions();

        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::FixedUpdate END ===");
        #endif
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

            // Assuming currentSteeringAngle is updated elsewhere
            //Quaternion targetRotation = Quaternion.Euler(0, 0.64f * Mathf.Rad2Deg, 0);
            //transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);


            if (agentInstances.ContainsKey(agentId))
            {
                // Agent already exists, update its position and rotation
                Debug.Log($"Agent ID: {agentId} already exists. Updating position and rotation.");

                var existingAgent = agentInstances[agentId]; // type TrafficAgent
                existingAgent.transform.position = position;
                existingAgent.transform.rotation = rotation;
                existingAgent.transform.hasChanged = true;
                Debug.Log($"Updated agent: {agentId} Position: {position}, Rotation: {rotation.eulerAngles}");

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
                //Debug.Log($"Instantiated agent: {agentId}, Prefab name: {agentPrefab.name}");

                var agent = agentObject.GetComponent<TrafficAgent>(); // type TrafficAgent

                if (agent == null)
                {
                    Debug.LogWarning($"TrafficAgent component not found on the instantiated prefab for {agentId}. Adding it manually.");
                    agent = agentObject.AddComponent<TrafficAgent>();
                }

                agent.Initialize();
                agentInstances.Add(agentId, agent);

                UpdateColliderForExistingAgent(agentObject, agentId);
            }

        }

        #if UNITY_EDITOR
        Debug.Log("=== TrafficManager::UpdateAgentPositions END ===");
        #endif
    }

    private void OnInitialAgentCountChanged(float newValue)
    {
        int newAgentCount = Mathf.RoundToInt(newValue);

        #if UNITY_EDITOR
        Debug.Log($"Received new initial agent count: {newAgentCount}");
        #endif

        if (newAgentCount != initialAgentCount)
        {
            initialAgentCount = newAgentCount;
            RestartSimulation();
        }
    }

    private void RestartSimulation()
    {
        // Clean up existing simulation
        OnDestroy();

        // Recreate the simulation with the new agent count
        trafficSimulationPtr = Traffic_create(initialAgentCount, seed);

        if (trafficSimulationPtr == IntPtr.Zero)
        {
            Debug.LogError("Failed to create new traffic simulation.");
            enabled = false;
            return;
        }

        // Reinitialize agent positions, velocities, etc.
        agentPositionsMap = Traffic_get_agent_positions(trafficSimulationPtr);
        agentVelocitiesMap = Traffic_get_agent_velocities(trafficSimulationPtr);
        agentOrientationsMap = Traffic_get_agent_orientations(trafficSimulationPtr);
        agentPreviousPositionsMap = Traffic_get_previous_positions(trafficSimulationPtr);

        // Reinitialize agents
        InitializeAgents();
    }

    private void CleanUpSimulation()
    {
        // Clean up existing agents
        foreach (var agentInstance in agentInstances.Values)
        {
            if (agentInstance != null)
            {
                Destroy(agentInstance.gameObject);
            }
        }
        agentInstances.Clear();
        agentColliders.Clear();

        // Clean up existing simulation
        if (trafficSimulationPtr != IntPtr.Zero)
        {
            Traffic_destroy(trafficSimulationPtr);
            trafficSimulationPtr = IntPtr.Zero;
        }

        // Clean up map references
        CleanUpMap(ref agentPositionsMap);
        CleanUpMap(ref agentVelocitiesMap);
        CleanUpMap(ref agentOrientationsMap);
        CleanUpMap(ref agentPreviousPositionsMap);
    }

    private void OnDestroy()
    {
        #if UNITY_EDITOR
        Debug.Log("-- TrafficManager::OnDestroy --");
        #endif

        CleanUpSimulation();

        // Unregister the channel when the TrafficManager is destroyed
        if (floatPropertiesChannel != null)
        {
            SideChannelManager.UnregisterSideChannel(floatPropertiesChannel);
        }
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

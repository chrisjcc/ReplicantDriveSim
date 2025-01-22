using System; // IntPtr
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.SideChannels; // to use FloatPropertiesChannel

using CustomSideChannel; // to use FieldValueChannel


// Responsible for stepping the traffic simulation and updating all agents
public class TrafficManager : MonoBehaviour
{
    // Constants
    private const string DllName = "ReplicantDriveSim";


    // Serialized Fields for Unity Editor (Unity Inspector variables)
    [SerializeField] private GameObject agentPrefab; // e.g., NISSAN-GTR)

    [SerializeField] private float simTimeStep = 0.04f;
    [SerializeField] private int initialAgentCount = 1;

    [SerializeField] public float maxSpeed = 1.0f; //60.0f;
    [SerializeField] private uint seed = 42;

    // Public Properties
    [SerializeField] public bool debugVisualization = false;

    [SerializeField] public int MaxSteps = 2000;

    //[HideInInspector] public float MoveSpeed { get; set; } = 5f;
    //[HideInInspector] public float RotationSpeed { get; set; } = 100f;
    [HideInInspector] public float AngleStep { get; private set; }

    [HideInInspector] public string TrafficAgentLayerName { get; set; } = "Road";
    [HideInInspector] public RayPerceptionSensorComponent3D raySensor;

    // Public Fields
    public IntPtr trafficSimulationPtr;
    public Dictionary<string, TrafficAgent> agentInstances = new Dictionary<string, TrafficAgent>();
    public Dictionary<string, Collider> agentColliders = new Dictionary<string, Collider>();

    // Private Fields
    private IntPtr agentPositionsMap;
    private IntPtr agentVelocitiesMap;
    private IntPtr agentOrientationsMap;
    private IntPtr agentPreviousPositionsMap;
    private List<int> highLevelActions;
    private List<float[]> lowLevelActions;
    private FloatPropertiesChannel floatPropertiesChannel;
    private FieldValueChannel sideChannel;
    private bool isDisposed = false;
    private bool hasCleanedUp = false;

    // DllImport Declarations
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Traffic_create(int num_agents, uint seed);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Traffic_destroy(IntPtr traffic);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr Traffic_sampleAndInitializeAgents(IntPtr traffic);

    // Note: Below SizeParamIndex is set 4, since it's the 4th parameter,
    // int low_level_actions_count, is the one that contains the size of the low_level_actions array.
    // This information is used by the .NET runtime to properly marshal the float[] array from the managed C# code to the unmanaged C++ code.
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr Traffic_step(IntPtr traffic,
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
    public static extern IntPtr Traffic_get_agent_by_name(IntPtr traffic, string name);

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
    public static extern float Vehicle_getYaw(IntPtr vehicle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int Vehicle_getId(IntPtr vehicle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vehicle_setSteering(IntPtr vehicle, float angle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void Vehicle_setYaw(IntPtr vehicle, float angle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr VehiclePtrVector_get(IntPtr vector, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int VehiclePtrVector_size(IntPtr vector);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int StringFloatVectorMap_size(IntPtr map);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr StringFloatVectorMap_get_key(IntPtr map, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr StringFloatVectorMap_get_value(IntPtr map, IntPtr key);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void StringFloatVectorMap_destroy(IntPtr map);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int FloatVector_size(IntPtr vector);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float FloatVector_get(IntPtr vector, int index);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FloatVector_destroy(IntPtr vector);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void FreeString(IntPtr str);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern float Traffic_getTimeStep(IntPtr traffic);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Traffic_setTimeStep(IntPtr traffic, float simTimeStep);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern float Traffic_getMaxVehicleSpeed(IntPtr traffic);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void Traffic_setMaxVehicleSpeed(IntPtr traffic, float maxSpeed);

    /// <summary>
    /// Initializes the TrafficManager component when the script instance is being loaded.
    /// This method is called only once during the lifetime of the script instance.
    /// It performs the following tasks:
    /// 1. Validates the agent prefab
    /// 2. Initializes fields
    /// 3. Sets up the float properties channel
    /// Debug logs are included in the Unity Editor for tracking the execution process.
    /// </summary>
    private void Awake()
    {
        LogDebug("TrafficManager::Awake started.");

        if (!ValidateAgentPrefab())
        {
            return;
        }

        InitializeFields();
        SetupFloatPropertiesChannel();

        LogDebug("TrafficManager::Awake completed successfully.");
    }

    /// <summary>
    /// Validates the agent prefab to ensure it meets the required criteria.
    /// This method performs the following checks:
    /// 1. Verifies that the agentPrefab is assigned
    /// 2. Checks if the agentPrefab has the necessary TrafficAgent component
    ///
    /// If any validation fails, it logs an error message, disables the component,
    /// and returns false. Otherwise, it returns true.
    ///
    /// Returns:
    ///     bool: True if the agent prefab is valid, false otherwise.
    /// </summary>
    private bool ValidateAgentPrefab()
    {
        if (agentPrefab == null)
        {
            Debug.LogError("Agent prefab is not assigned. Please assign it in the inspector.");
            enabled = false;
            return false;
        }

        // Validate that the prefab has necessary components
        if (!agentPrefab.GetComponent<TrafficAgent>())
        {
            Debug.LogError("Agent prefab does not have a TrafficAgent component. Please add one.");
            enabled = false;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Initializes various fields used by the TrafficManager.
    /// This method performs the following tasks:
    /// 1. Initializes the highLevelActions list
    /// 2. Initializes the lowLevelActions list
    ///
    /// This method should be called during the setup phase, typically in the Awake() method.
    /// </summary>
    private void InitializeFields()
    {
        highLevelActions = new List<int>();
        lowLevelActions = new List<float[]>();
    }

    /// <summary>
    /// Sets up the FloatPropertiesChannel for communication with the ML-Agents training environment.
    /// This method performs the following tasks:
    /// 1. Creates a new FloatPropertiesChannel with a specific GUID
    /// 2. Registers the channel with the SideChannelManager
    /// 3. Subscribes to the "initialAgentCount" property change event
    ///
    /// Note: The method includes commented-out code for getting the initialAgentCount
    /// from environment parameters, which may be used in future implementations.
    ///
    /// This method is crucial for enabling runtime parameter adjustments and
    /// should be called during the initialization phase, typically in the Awake() method.
    /// </summary>
    private void SetupFloatPropertiesChannel()
    {
        LogDebug("TrafficManager::SetupFloatPropertiesChannel started");

        // Create the FloatPropertiesChannel
        Guid floatPropertiesChannelGuid = new Guid("621f0a70-4f87-11ea-a6bf-784f4387d1f7");

        // Create the float properties channel
        floatPropertiesChannel = new FloatPropertiesChannel(floatPropertiesChannelGuid);

        // Register the channel
        SideChannelManager.RegisterSideChannel(floatPropertiesChannel);

        // Subscribe to the MaxSteps envet
        floatPropertiesChannel.RegisterCallback("MaxSteps", MaxEpisodeSteps);

        // Inject a custom Guid at instantiation or use the default one
        Guid sideChannelGuid = new Guid("621f0a70-4f87-11ea-a6bf-784f4387d1f8");

        // Create the FieldValueChannel
        sideChannel = new FieldValueChannel(sideChannelGuid);
        SideChannelManager.RegisterSideChannel(sideChannel);

        // Sending a field value (e.g., current FPS)
        sideChannel.SendFieldValue("FramesPerSecond", 1.0f / simTimeStep);

        // Get the initialAgentCount parameter from the environment parameters
        var envParameters = Academy.Instance.EnvironmentParameters;
        initialAgentCount = Mathf.RoundToInt(envParameters.GetWithDefault("initialAgentCount", 1.0f));

        LogDebug("TrafficManager::SetupFloatPropertiesChannel completed");
    }

    /// <summary>
    /// Initializes the traffic simulation by creating a new instance using the C API.
    /// This method performs the following tasks:
    /// 1. Calls the Traffic_create function from the C API to create a new traffic simulation
    /// 2. Stores the returned pointer to the traffic simulation
    /// 3. Throws an exception if the creation fails (pointer is null)
    /// 4. Updates the agent maps after successful creation
    ///
    /// Debug logs are included in the Unity Editor for tracking the execution process.
    ///
    /// This method is crucial for setting up the core traffic simulation and should be
    /// called during the initialization phase of the TrafficManager.
    ///
    /// Throws:
    ///     InvalidOperationException: If the traffic simulation creation fails.
    /// </summary>
    private void InitializeTrafficSimulation()
    {
        LogDebug("Attempting to create traffic simulation.");

        // Assuming you have a reference to the Traffic_create and Traffic_destroy functions from the C API
        trafficSimulationPtr = CreateSimulation();

        // Set initial values from Unity Editor to the C++ simulation
        Traffic_setTimeStep(trafficSimulationPtr, simTimeStep);
        Traffic_setMaxVehicleSpeed(trafficSimulationPtr, maxSpeed);

        GetSimulationData();
    }

    /// <summary>
    /// Clears all existing agent instances from the scene and internal data structures.
    /// This method performs the following tasks:
    /// 1. Iterates through all agent instances in the agentInstances dictionary
    /// 2. Destroys the GameObject of each agent if it exists
    /// 3. Clears the agentInstances dictionary
    /// 4. Clears the agentColliders dictionary
    ///
    /// This method is typically called before reinitializing the traffic simulation
    /// or when resetting the TrafficManager to ensure a clean slate.
    /// It helps prevent duplicate agents and ensures proper cleanup of resources.
    ///
    /// Note: This method assumes that agent GameObjects are the direct parents of any
    /// child objects that need to be destroyed along with the agent.
    /// </summary>
    private void ClearExistingAgents()
    {
        // Clear existing agents before initializing
        foreach (var agent in agentInstances.Values)
        {
            if (agent != null && agent.gameObject != null)
            {
                Destroy(agent.gameObject);
            }
        }
        agentInstances.Clear();
        agentColliders.Clear();
    }

    /// <summary>
    /// Logs the results of agent initialization for debugging purposes.
    /// This method outputs the following information to the Unity console:
    /// 1. The total number of agent instances created
    /// 2. The total number of collider boxes associated with agents
    ///
    /// These log messages are only displayed in the Unity Editor, not in builds.
    /// This method is useful for verifying that the correct number of agents
    /// and colliders have been initialized, helping to catch potential issues
    /// early in the development process.
    ///
    /// Typically called after agent initialization or reset operations.
    /// </summary>
    private void LogAgentInitializationResults()
    {
        LogDebug($"Number of agents: {agentInstances.Count}");
        LogDebug($"Number of collider boxes: {agentColliders.Count}");
    }

    /// <summary>
    /// Retrieves the agent ID as a string for a given index from the agentPositionsMap.
    /// This method uses the C API to access the key (agent ID) from the StringFloatVectorMap.
    ///
    /// The method performs the following steps:
    /// 1. Calls StringFloatVectorMap_get_key to get a pointer to the agent ID string
    /// 2. Checks if the returned pointer is valid
    /// 3. Converts the pointer to a managed string using Marshal.PtrToStringAnsi
    ///
    /// This method is crucial for maintaining the link between the C++ simulation
    /// and the Unity representation of agents.
    ///
    /// </summary>
    /// <param name="index">The index of the agent in the agentPositionsMap</param>
    /// <returns>The agent ID as a string</returns>
    /// <exception cref="InvalidOperationException">Thrown if the agent ID retrieval fails</exception>
    private string GetAgentId(int index)
    {
        IntPtr agentIdPtr = StringFloatVectorMap_get_key(agentPositionsMap, index);
        if (agentIdPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException($"Failed to get agent ID for index {index}");
        }
        return Marshal.PtrToStringAnsi(agentIdPtr);
    }

    /// <summary>
    /// Retrieves the current position of an agent as a Vector3 based on its ID.
    /// This method interfaces with the C++ simulation data through the C API.
    ///
    /// The method performs the following steps:
    /// 1. Converts the agent ID to an unmanaged string pointer
    /// 2. Retrieves the position data from the agentPositionsMap using the C API
    /// 3. Converts the retrieved float vector to a Unity Vector3
    /// 4. Ensures proper cleanup of unmanaged resources
    ///
    /// This method is essential for updating the Unity representation of agents
    /// with their current positions from the simulation.
    ///
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent</param>
    /// <returns>A Vector3 representing the agent's current position</returns>
    /// <exception cref="InvalidOperationException">Thrown if the position retrieval fails</exception>
    private Vector3 GetAgentPosition(string agentId)
    {
        IntPtr agentIdPtr = Marshal.StringToHGlobalAnsi(agentId);
        try
        {
            IntPtr positionPtr = StringFloatVectorMap_get_value(agentPositionsMap, agentIdPtr);

            if (positionPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to get position for agent {agentId}");
            }
            return GetVector3FromFloatVector(positionPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(agentIdPtr);
        }

    }

    /// <summary>
    /// Retrieves the current rotation of an agent as a Quaternion based on its ID.
    /// This method interfaces with the C++ simulation data through the C API.
    ///
    /// The method performs the following steps:
    /// 1. Converts the agent ID to an unmanaged string pointer
    /// 2. Retrieves the orientation data from the agentOrientationsMap using the C API
    /// 3. Converts the retrieved float vector to a Unity Quaternion
    /// 4. Ensures proper cleanup of unmanaged resources
    ///
    /// This method is crucial for updating the Unity representation of agents
    /// with their current rotations from the simulation, ensuring visual accuracy.
    ///
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent</param>
    /// <returns>A Quaternion representing the agent's current rotation</returns>
    /// <exception cref="InvalidOperationException">Thrown if the orientation retrieval fails</exception>
    private Quaternion GetAgentRotation(string agentId)
    {
        IntPtr agentIdPtr = Marshal.StringToHGlobalAnsi(agentId);
        try
        {
            IntPtr orientationPtr = StringFloatVectorMap_get_value(agentOrientationsMap, agentIdPtr);

            if (orientationPtr == IntPtr.Zero)
            {
                throw new InvalidOperationException($"Failed to get orientation for agent {agentId}");
            }
            return GetQuaternionFromFloatVector(orientationPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(agentIdPtr);
        }
    }

    /// <summary>
    /// Logs detailed information about an agent's position and rotation for debugging purposes.
    /// This method outputs the following information to the Unity console:
    /// 1. The agent's ID
    /// 2. The agent's position as a Vector3
    /// 3. The agent's rotation as Euler angles
    /// 4. A breakdown of the rotation into Pitch, Yaw, and Roll components
    ///
    /// These log messages are only displayed in the Unity Editor, not in builds.
    /// This method is useful for verifying the correct positioning and orientation of agents,
    /// helping to diagnose issues related to agent placement or movement.
    ///
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent</param>
    /// <param name="position">The current position of the agent as a Vector3</param>
    /// <param name="rotation">The current rotation of the agent as a Quaternion</param>
    private void LogAgentDetails(string agentId, Vector3 position, Quaternion rotation)
    {
        LogDebug($"Agent {agentId}: Position = {position}, Rotation = {rotation}");
        LogDebug($"Angles: Pitch={rotation.x}, Yaw={rotation.y}, Roll={rotation.z}");
    }

    /// <summary>
    /// Updates the position and rotation of an existing TrafficAgent in the Unity scene.
    /// This method is called when an agent already exists and needs to be synchronized
    /// with the latest data from the traffic simulation.
    ///
    /// The method performs the following actions:
    /// 1. Logs a debug message indicating the agent is being updated
    /// 2. Sets the agent's transform position to the new position
    /// 3. Sets the agent's transform rotation to the new rotation
    /// 4. Marks the agent's transform as changed
    ///
    /// Note: There's a commented-out call to UpdateColliderForExistingAgent,
    /// which might be needed for updating the agent's collider. Consider uncommenting
    /// and implementing this if collider updates are necessary.
    ///
    /// </summary>
    /// <param name="agent">The TrafficAgent instance to update</param>
    /// <param name="position">The new position for the agent</param>
    /// <param name="rotation">The new rotation for the agent</param>
    private void UpdateExistingAgent(TrafficAgent agent, Vector3 position, Quaternion rotation)
    {
        LogDebug($"Agent ID: {agent.name} already exists. Updating position and rotation.");

        agent.transform.position = position;
        agent.transform.rotation = rotation;
        agent.transform.hasChanged = true;

        LogDebug($"Updated agent: {agent.name} Position: {position}, Rotation: {rotation}");
    }

    /// <summary>
    /// Creates a new agent in the Unity scene based on data from the traffic simulation.
    /// This method is called when a new agent needs to be instantiated and added to the scene.
    ///
    /// The method performs the following actions:
    /// 1. Instantiates a new agent GameObject from the agentPrefab
    /// 2. Sets the name of the GameObject to the agent's ID
    /// 3. Sets the parent of the new agent to this TrafficManager's transform
    /// 4. Sets the appropriate layer for the agent GameObject
    /// 5. Adds or retrieves the TrafficAgent component and initializes it
    /// 6. Adds the new agent to the agentInstances dictionary
    /// 7. Updates the collider for the new agent
    ///
    /// This method is crucial for maintaining synchronization between the traffic simulation
    /// and the Unity representation, ensuring that new agents are properly created and set up.
    ///
    /// </summary>
    /// <param name="agentId">The unique identifier for the new agent</param>
    /// <param name="position">The initial position for the new agent</param>
    /// <param name="rotation">The initial rotation for the new agent</param>
    private void CreateNewAgent(string agentId, Vector3 position, Quaternion rotation)
    {
        GameObject agentObject = Instantiate(agentPrefab, position, rotation);
        agentObject.name = agentId;
        agentObject.transform.SetParent(this.transform);

        SetAgentLayer(agentObject);

        // Add RayPerceptionSensor to the traffic agent
        AddRaySensorToAgent(agentObject);

        TrafficAgent agent = GetOrAddTrafficAgentComponent(agentObject);

        agent.Initialize();
        agentInstances.Add(agentId, agent);
        UpdateColliderForExistingAgent(agentObject, agentId);
    }

    /// <summary>
    /// Sets the layer of the given agent GameObject to the predefined TrafficAgent layer.
    /// This method ensures that all traffic agents are on the correct layer for proper
    /// rendering, collision detection, and other layer-based functionality in Unity.
    ///
    /// The method performs the following actions:
    /// 1. Converts the TrafficAgentLayerName to its corresponding layer index
    /// 2. Sets the agent GameObject's layer to this index
    /// 3. Logs the layer assignment in the Unity Editor for debugging purposes
    ///
    /// Proper layer assignment is crucial for:
    /// - Collision detection and physics interactions
    /// - Camera culling and rendering optimizations
    /// - Raycasting and other layer-based operations
    ///
    /// </summary>
    /// <param name="agentObject">The GameObject of the traffic agent to set the layer for</param>
    private void SetAgentLayer(GameObject agentObject)
    {
        agentObject.layer = LayerMask.NameToLayer(TrafficAgentLayerName);

        LogDebug($"Set layer for agent: {agentObject.name}, layer index: {agentObject.layer}");
    }

    /// <summary>
    /// Retrieves or adds a TrafficAgent component to the given agent GameObject.
    /// This method ensures that every agent in the simulation has the necessary TrafficAgent component.
    ///
    /// The method performs the following actions:
    /// 1. Attempts to get the TrafficAgent component from the agent GameObject
    /// 2. If the component doesn't exist, it adds a new TrafficAgent component
    /// 3. Sets the MaxStep property of the newly added TrafficAgent to 500
    /// 4. Logs a warning in the Unity Editor if a new component had to be added
    ///
    /// This method is crucial for maintaining consistency in agent behavior and properties,
    /// ensuring that all agents have the required components for proper functioning within
    /// the traffic simulation.
    ///
    /// </summary>
    /// <param name="agentObject">The GameObject to check or add the TrafficAgent component to</param>
    /// <returns>The existing or newly added TrafficAgent component</returns>
    private TrafficAgent GetOrAddTrafficAgentComponent(GameObject agentObject)
    {
        TrafficAgent agent = agentObject.GetComponent<TrafficAgent>();
        if (agent == null)
        {
            Debug.LogWarning($"TrafficAgent component not found on the instantiated prefab for {agentObject.name}. Adding it manually.");

            agent = agentObject.AddComponent<TrafficAgent>();
            agent.MaxStep = MaxSteps;
        }
        return agent;
    }

    /// <summary>
    /// Initializes the TrafficManager when the script instance is enabled just before any of the Update methods are called the first time.
    /// This method is responsible for setting up the traffic simulation and initializing agents.
    ///
    /// The method performs the following actions:
    /// 1. Initializes the traffic simulation by calling InitializeTrafficSimulation()
    /// 2. Initializes agents by calling InitializeAgents()
    /// 3. Catches and logs any exceptions that occur during initialization
    /// 4. Disables the TrafficManager component if an error occurs
    ///
    /// Debug logs are included at the start and end of the method execution in the Unity Editor for tracking the process.
    ///
    /// This method is crucial for setting up the initial state of the traffic simulation and should only be called once
    /// at the beginning of the scene execution.
    ///
    /// Note: If an exception occurs during initialization, the TrafficManager will be disabled to prevent further errors.
    /// </summary>
    private void Start()
    {
        LogDebug("TrafficManager::Start started.");

        try
        {
            InitializeTrafficSimulation();
            InitializeAgents();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in TrafficManager Start: {e.Message}\n{e.StackTrace}");
            enabled = false;
        }

        LogDebug("TrafficManager::Start completed successfully.");
    }

    /// <summary>
    /// Initializes the agents in the traffic simulation.
    /// This method is responsible for creating the initial set of agents in the Unity scene.
    ///
    /// The method performs the following actions:
    /// 1. Clears any existing agents from the scene using ClearExistingAgents()
    /// 2. Iterates through the desired number of agents (initialAgentCount)
    /// 3. Attempts to spawn each agent using SpawnAgent()
    /// 4. Catches and logs any exceptions that occur during individual agent spawning
    /// 5. Logs the results of the agent initialization process
    ///
    /// Debug logs are included at the start and end of the method execution in the Unity Editor for tracking the process.
    ///
    /// This method is crucial for populating the simulation with the initial set of agents.
    /// It ensures a clean slate by removing existing agents before creating new ones,
    /// and provides error handling for individual agent spawning failures.
    ///
    /// Note: If an individual agent fails to spawn, the method will log the error and continue with the next agent,
    /// allowing the initialization process to complete even if some agents fail to spawn.
    /// </summary>
    private void InitializeAgents()
    {
        LogDebug("TrafficManager::InitializeAgents started.");

        ClearExistingAgents();

        // Spawn new agents
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

        ValidateAgentCount();
        LogAgentInitializationResults();

        LogDebug("TrafficManager::InitializeAgents completed successfully.");
    }

    /// <summary>
    /// Spawns or updates an individual agent in the traffic simulation.
    /// This method is responsible for creating a new agent or updating an existing one
    /// based on the data from the traffic simulation.
    ///
    /// The method performs the following actions:
    /// 1. Retrieves the agent's ID using GetAgentId()
    /// 2. Gets the agent's position using GetAgentPosition()
    /// 3. Gets the agent's rotation using GetAgentRotation()
    /// 4. Logs the agent's details for debugging purposes
    /// 5. Checks if an agent with the given ID already exists
    ///    - If it exists, update its position and rotation
    ///    - If it doesn't exist, create a new agent
    ///
    /// This method is crucial for maintaining synchronization between the traffic simulation
    /// and the Unity representation of agents. It ensures that each agent in the simulation
    /// has a corresponding GameObject in the Unity scene with the correct position and rotation.
    ///
    /// </summary>
    private void SpawnAgent(int index)
    {
        string agentId = GetAgentId(index);
        Vector3 position = GetAgentPosition(agentId);
        Quaternion rotation = GetAgentRotation(agentId);

        LogAgentDetails(agentId, position, rotation);

        if (agentInstances.TryGetValue(agentId, out TrafficAgent existingAgent))
        {
            UpdateExistingAgent(existingAgent, position, rotation);
        }
        else
        {
            CreateNewAgent(agentId, position, rotation);
        }
    }

    /// <summary>
    /// Converts a C++ float vector (accessed via pointer) to a Unity Vector3.
    /// This method is crucial for translating position data from the C++ traffic simulation
    /// into a format usable in Unity.
    ///
    /// The method performs the following actions:
    /// 1. Check if the float vector has at least 3 elements
    /// 2. Retrieves the first three float values from the vector
    /// 3. Creates and returns a new Vector3 using these values
    ///
    /// This method ensures proper data conversion between the C++ simulation
    /// and Unity, maintaining accurate positional data for agents.
    ///
    /// </summary>
    /// <param name="vectorPtr">Pointer to the C++ float vector</param>
    /// <returns>A Unity Vector3 created from the first three elements of the float vector</returns>
    /// <exception cref="Exception">Thrown if the float vector has fewer than 3 elements</exception>
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

    /// <summary>
    /// Converts a C++ float vector (accessed via pointer) containing Euler angles to a Unity Quaternion.
    /// This method is crucial for translating rotation data from the C++ traffic simulation
    /// into a format usable in Unity.
    ///
    /// The method performs the following actions:
    /// 1. Check if the float vector has at least 3 elements (for roll, pitch, and yaw)
    /// 2. Retrieves the first three float values from the vector as Euler angles in radians
    /// 3. Converts the Euler angles from radians to degrees
    /// 4. Creates and returns a Unity Quaternion using these Euler angles
    ///
    /// Note: The method assumes the Euler angles are in the order: roll (X-axis), pitch (Y-axis), yaw (Z-axis).
    /// It also handles the conversion from radians to degrees, as Unity's Quaternion.Euler method expects degrees.
    ///
    /// This method ensures proper conversion of rotation data between the C++ simulation
    /// and Unity, maintaining accurate orientation data for agents.
    ///
    /// </summary>
    /// <param name="vectorPtr">Pointer to the C++ float vector containing Euler angles in radians</param>
    /// <returns>A Unity Quaternion created from the Euler angles in the float vector</returns>
    /// <exception cref="Exception">Thrown if the float vector has fewer than 3 elements</exception>
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

    /// <summary>
    /// Updates or adds a collider to an existing agent GameObject in the traffic simulation.
    /// This method ensures that each agent has an appropriately configured collider for
    /// physics interactions and collision detection.
    ///
    /// The method performs the following actions:
    /// 1. Retrieves or adds a collider component to the agent GameObject
    /// 2. Configures the collider with appropriate settings
    /// 3. Updates the internal dictionary that tracks agent colliders
    ///
    /// Debug logs are included at the start and end of the method execution in the Unity Editor to track the process.
    ///
    /// This method is crucial for maintaining accurate physics representations of agents in the Unity scene,
    /// which is essential for realistic traffic simulation and collision detection.
    ///
    /// </summary>
    /// <param name="agentObject">The GameObject of the agent to update or add a collider to</param>
    /// <param name="agentId">The unique identifier of the agent</param>
    private void UpdateColliderForExistingAgent(GameObject agentObject, string agentId)
    {
        LogDebug("TrafficManager::UpdateColliderForExistingAgent started.");

        Collider agentCollider = GetOrAddCollider(agentObject, agentId);
        ConfigureCollider(agentCollider);
        UpdateColliderDictionary(agentId, agentCollider);

        LogDebug("TrafficManager::UpdateColliderForExistingAgent completed successfully.");
    }

    /// <summary>
    /// Retrieves an existing collider or adds a new one to the agent GameObject.
    /// This method ensures that each agent has a collider component for physics interactions.
    ///
    /// The method performs the following actions:
    /// 1. Searches for an existing collider on the agent GameObject or its children recursively
    /// 2. If no collider is found, it logs a warning and adds a new collider to the agent
    /// 3. Returns the found or newly added collider
    ///
    /// This method is crucial for maintaining proper physics representations of agents in the Unity scene.
    /// It handles cases where colliders might be on child objects or missing entirely, ensuring
    /// consistent collision detection for all agents.
    ///
    /// </summary>
    /// <param name="agentObject">The GameObject of the agent to check or add a collider to</param>
    /// <param name="agentId">The unique identifier of the agent, used for logging purposes</param>
    /// <returns>The existing or newly added Collider component</returns>
    private Collider GetOrAddCollider(GameObject agentObject, string agentId)
    {
        // Get the collider component from the instantiated agent or its children recursively
        Collider agentCollider = FindColliderRecursively(agentObject);

        if (agentCollider == null)
        {
            LogWarningAndAddCollider(agentObject, agentId);
            agentCollider = agentObject.GetComponent<Collider>();
        }

        return agentCollider;
    }

    /// <summary>
    /// Logs a warning and adds a BoxCollider to an agent GameObject when no existing collider is found.
    /// This method serves as a fallback mechanism to ensure all agents have collision detection capabilities.
    ///
    /// The method performs the following actions:
    /// 1. Logs a warning message to the Unity console, indicating that no collider was found for the specific agent
    /// 2. Adds a BoxCollider component to the agent GameObject
    ///
    /// This method is crucial for maintaining the integrity of the physics simulation by ensuring
    /// that every agent has a collider, even if one was not initially present in the prefab or model.
    /// The warning log helps developers identify and potentially address issues with agent prefabs or models
    /// that are missing expected colliders.
    ///
    /// </summary>
    /// <param name="agentObject">The GameObject of the agent to add a BoxCollider to</param>
    /// <param name="agentId">The unique identifier of the agent, used in the warning message for easy identification</param>
    private void LogWarningAndAddCollider(GameObject agentObject, string agentId)
    {
        Debug.LogWarning($"No Collider found on the agent {agentId} or its children. Adding a BoxCollider.");
        agentObject.AddComponent<BoxCollider>();
    }

    /// <summary>
    /// Configures the properties of a given collider for use in the traffic simulation.
    /// This method ensures that the collider is set up correctly for physical interactions.
    ///
    /// The method performs the following action:
    /// 1. Sets the isTrigger property of the collider to false, if the collider is not null
    ///
    /// Setting isTrigger to false ensures that the collider will cause physical collisions
    /// rather than just triggering events. This is crucial for realistic traffic simulation
    /// where agents should physically interact with each other and the environment.
    ///
    /// The null check prevents potential NullReferenceExceptions if an invalid collider is passed.
    ///
    /// While currently this method only sets the isTrigger property, it provides a centralized
    /// place for any future collider configurations that may be needed for the traffic simulation.
    ///
    /// </summary>
    /// <param name="collider">The Collider component to be configured</param>
    private void ConfigureCollider(Collider collider)
    {
        if (collider != null)
        {
            collider.isTrigger = false;
        }
    }

    /// <summary>
    /// Updates the internal dictionary that maps agent IDs to their corresponding colliders.
    /// This method ensures that the TrafficManager maintains an up-to-date record of all agent colliders.
    ///
    /// The method performs the following actions:
    /// 1. Adds or updates the entry in the agentColliders dictionary for the given agent ID
    /// 2. Logs a debug message in the Unity Editor with the agent ID and collider type
    ///
    /// Maintaining this dictionary is crucial for efficient access to agent colliders,
    /// which can be useful for various operations such as collision checks, physics raycasts,
    /// or any custom logic that needs to interact with specific agent colliders.
    ///
    /// The debug log in the Unity Editor helps track collider updates and can be useful
    /// for debugging or verifying that agents have the correct collider types assigned.
    ///
    /// </summary>
    /// <param name="agentId">The unique identifier of the agent</param>
    /// <param name="agentCollider">The Collider component associated with the agent</param>
    private void UpdateColliderDictionary(string agentId, Collider agentCollider)
    {
        agentColliders[agentId] = agentCollider;

        LogDebug($"Updated collider for Agent ID: {agentId}. Collider type: {agentCollider.GetType().Name}");
    }

    /// <summary>
    /// Recursively searches for a Collider component in the given GameObject and its children.
    /// This helper method is used to find colliders that may be attached to child objects of an agent.
    ///
    /// The method performs the following actions:
    /// 1. Checks if the given GameObject has a Collider component
    /// 2. If a Collider is found on the object, it returns that Collider
    /// 3. If no Collider is found, it recursively searches through all child objects
    /// 4. Returns the first Collider found in the hierarchy, or null if no Collider is found
    ///
    /// This method is crucial for handling complex agent models where the Collider might not be
    /// on the root GameObject but on one of its children. It ensures that existing Colliders
    /// are properly detected and utilized before new ones are added unnecessarily.
    ///
    /// Debug logs are included (commented out) for detailed tracking of the search process,
    /// which can be useful for troubleshooting Collider detection issues.
    ///
    /// </summary>
    /// <param name="obj">The GameObject to search for a Collider</param>
    /// <returns>The first Collider found in the GameObject or its children, or null if no Collider is found</returns>
    private Collider FindColliderRecursively(GameObject obj)
    {
        LogDebug($"TrafficManager::FindColliderRecursively started.");

        if (obj == null)
        {
            Debug.LogWarning("Null GameObject passed to FindColliderRecursively");
            return null;
        }

        LogSearchStart(obj);

        // Check if the object itself has a Collider
        Collider collider = obj.GetComponent<Collider>();

        if (collider != null)
        {
            LogColliderFound(obj);
            return collider;
        }

        LogDebug("TrafficManager::FindColliderRecursively completed successfully.");

        return SearchChildrenForCollider(obj);
    }

    /// <summary>
    /// Logs the start of a collider search operation for a specific GameObject.
    /// This method is used for debugging purposes to track the process of searching for colliders in the scene hierarchy.
    ///
    /// The method performs the following action:
    /// 1. Logs a debug message indicating the start of a collider search for the given GameObject
    ///
    /// Key aspects of this method:
    /// - It's a helper method used for debugging and tracing the collider search process
    /// - The log message includes the name of the GameObject being searched
    /// - The logging is only active in the Unity Editor, not in builds
    /// - It helps in visualizing the traversal of the scene hierarchy during collider searches
    ///
    /// Note: The debug log is wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring it only appears in the Unity Editor and not in builds, thus not affecting release performance.
    ///
    /// This method is typically called at the beginning of a recursive search for colliders,
    /// providing visibility into which GameObjects are being examined during the search process.
    ///
    /// Usage of this method can be beneficial for:
    /// - Debugging issues related to missing or incorrectly placed colliders
    /// - Understanding the order and depth of GameObject traversal during collider searches
    /// - Optimizing collider search processes by identifying unnecessary searches
    ///
    /// Developers should use this method judiciously, as excessive logging can impact editor performance
    /// during debugging sessions with complex hierarchies or frequent collider searches.
    /// </summary>
    /// <param name="obj">The GameObject for which the collider search is starting</param>
    private void LogSearchStart(GameObject obj)
    {
        LogDebug($"Searching for Collider in: {obj.name}");
    }

    /// <summary>
    /// Logs information about colliders found on a specific GameObject and its children.
    /// This method is used for debugging purposes to provide detailed information about collider detection results.
    ///
    /// The method performs the following actions:
    /// 1. Logs a debug message indicating that a collider was found on the given GameObject
    /// 2. Retrieves all colliders from the children of the GameObject (including inactive children)
    /// 3. Logs the number of colliders found in the children of the GameObject
    ///
    /// Key aspects of this method:
    /// - It's a helper method used for debugging and verifying collider detection results
    /// - The log messages include the name of the GameObject and the number of colliders found in its children
    /// - The logging is only active in the Unity Editor, not in builds
    /// - It provides a comprehensive view of the collider structure for a given GameObject and its hierarchy
    ///
    /// Note: The debug logs are wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring they only appear in the Unity Editor and not in builds, thus not affecting release performance.
    ///
    /// This method is typically called when a collider is found during a search process,
    /// offering detailed information about the collider structure of the GameObject and its children.
    ///
    /// Usage of this method can be beneficial for:
    /// - Debugging issues related to unexpected collider behavior
    /// - Verifying the correct setup of colliders in complex prefabs or hierarchies
    /// - Understanding the distribution of colliders in a GameObject hierarchy
    /// - Identifying potential optimization opportunities in collider setups
    ///
    /// Developers should use this method judiciously, as it performs a potentially expensive operation
    /// (GetComponentsInChildren) and logs multiple messages, which could impact editor performance
    /// if used excessively, especially with complex hierarchies.
    /// </summary>
    /// <param name="obj">The GameObject on which a collider was found and whose children will be checked for additional colliders</param>
    private void LogColliderFound(GameObject obj)
    {
        LogDebug($"Found Collider on: {obj.name}");
        Collider[] childColliders = obj.GetComponentsInChildren<Collider>(true);
        LogDebug($"Found {childColliders.Length} Collider(s) in children of: {obj.name}");
    }

    /// <summary>
    /// Recursively searches for a Collider component in the children of the given GameObject.
    /// This method performs a depth-first search through the hierarchy to find the first collider.
    ///
    /// The method performs the following actions:
    /// 1. Iterates through each child of the given GameObject
    /// 2. Recursively calls FindColliderRecursively on each child
    /// 3. Returns the first non-null collider found in the hierarchy
    /// 4. If no collider is found, logs the result and returns null
    ///
    /// Key aspects of this method:
    /// - It's a helper method used in the broader collider search process
    /// - The search is depth-first, meaning it fully explores each child branch before moving to siblings
    /// - It stops and returns immediately upon finding the first collider
    /// - If no collider is found in any child, it logs this information using LogNoColliderFound
    ///
    /// This method is typically called as part of a larger collider search operation,
    /// specifically to handle cases where a collider might be on a child object rather than the parent.
    ///
    /// Usage of this method is important for:
    /// - Ensuring thorough collider detection in complex hierarchies
    /// - Handling cases where colliders are placed on child objects for organizational purposes
    /// - Providing a comprehensive search that doesn't miss colliders due to hierarchy structure
    ///
    /// Note: The performance of this method can be impacted by the depth and breadth of the object hierarchy.
    /// For very large or deep hierarchies, consider optimizing the search process or caching results if appropriate.
    ///
    /// </summary>
    /// <param name="obj">The GameObject whose children will be searched for a Collider</param>
    /// <returns>The first Collider found in the hierarchy, or null if no Collider is found</returns>
    private Collider SearchChildrenForCollider(GameObject obj)
    {
        foreach (Transform child in obj.transform)
        {
            Collider childCollider = FindColliderRecursively(child.gameObject);
            if (childCollider != null)
            {
                return childCollider;
            }
        }

        LogNoColliderFound(obj);
        return null;
    }

    /// <summary>
    /// Logs a debug message when no Collider is found on a GameObject or its children.
    /// This method is used for debugging purposes to track unsuccessful collider searches.
    ///
    /// The method performs the following action:
    /// 1. Logs a debug message indicating that no collider was found on the given GameObject or its children
    ///
    /// Key aspects of this method:
    /// - It's a helper method used for debugging and tracing the collider search process
    /// - The log message includes the name of the GameObject that was searched
    /// - The logging is only active in the Unity Editor, not in builds
    /// - It helps in identifying GameObjects that unexpectedly lack colliders
    ///
    /// Note: The debug log is wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring it only appears in the Unity Editor and not in builds, thus not affecting release performance.
    ///
    /// This method is typically called at the end of a recursive search for colliders
    /// when no collider has been found in the entire hierarchy of the given GameObject.
    ///
    /// Usage of this method can be beneficial for:
    /// - Debugging issues related to missing colliders in the scene
    /// - Identifying GameObjects that may require colliders to be added
    /// - Verifying the completeness of collider setups in complex prefabs or scenes
    /// - Assisting in the process of ensuring all necessary objects have proper collision detection
    ///
    /// Developers should use this information to:
    /// - Identify and fix instances where colliders are unexpectedly missing
    /// - Verify that the lack of a collider is intentional for specific GameObjects
    /// - Optimize scenes by ensuring colliders are present only where necessary
    ///
    /// While this method is valuable for debugging, developers should be mindful of log clutter
    /// in complex scenes with many intentionally collider-less objects.
    /// </summary>
    /// <param name="obj">The GameObject for which no collider was found in its hierarchy</param>
    private void LogNoColliderFound(GameObject obj)
    {
        LogDebug($"No Collider found in: {obj.name} or its children");
    }

    /// <summary>
    /// Generates the full hierarchical path of a GameObject in the Unity scene.
    /// This helper method constructs a string representation of the GameObject's position in the hierarchy.
    ///
    /// The method performs the following actions:
    /// 1. Starts with the name of the given GameObject
    /// 2. Iteratively traverses up the hierarchy, prepending each parent's name
    /// 3. Constructs a path string using '/' as a separator between hierarchy levels
    /// 4. Continues until it reaches the root of the hierarchy (i.e., an object with no parent)
    ///
    /// This method is useful for:
    /// - Debugging: Providing clear, identifiable paths for GameObjects in log messages
    /// - Hierarchy Analysis: Understanding the structure of complex prefabs or scene setups
    /// - Unique Identification: Creating unique identifiers for GameObjects based on their hierarchy
    ///
    /// The resulting path string format is: "RootObject/ParentObject/ChildObject/GivenObject"
    ///
    /// </summary>
    /// <param name="obj">The GameObject whose path is to be determined</param>
    /// <returns>A string representing the full hierarchical path of the GameObject</returns>
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

    /// <summary>
    /// Executes the core simulation loop at a fixed time step (main physics simulation stepping).
    /// This method is called by Unity at a fixed interval, independent of frame rate, making it ideal for physics and simulation updates.
    ///
    /// The method performs the following actions in sequence:
    /// 1. Validates the current state of the simulation
    /// 2. Collects actions from all active agents
    /// 3. Advances the simulation by one step
    /// 4. Updates the positions of all agents in the Unity scene
    ///
    /// Key aspects of this method:
    /// - It's part of Unity's MonoBehaviour lifecycle, called at a fixed time interval
    /// - Ensures the simulation state is valid before proceeding
    /// - Coordinates the core loop of the traffic simulation: input collection, simulation step, and visual update
    /// - Uses separate methods for each major step, promoting modularity and easier maintenance
    /// - Includes a debug log in the Unity Editor to confirm successful completion of the update
    ///
    /// This method is crucial for:
    /// - Maintaining consistent and predictable simulation behavior regardless of frame rate
    /// - Synchronizing the internal simulation state with Unity's physics engine
    /// - Ensuring smooth and accurate traffic flow in the simulation
    ///
    /// Note: The debug log is wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring it only appears in the Unity Editor and not in builds, avoiding performance impact in released versions.
    ///
    /// Developers should be aware that:
    /// - Any heavy computations in this method could impact the overall performance of the simulation
    /// - The frequency of FixedUpdate calls can be adjusted in Unity's Time settings, which will affect the simulation speed
    /// - Proper error handling and state validation are crucial to prevent cascading issues in the simulation
    ///
    /// Optimization of the methods called within FixedUpdate is key to maintaining high performance,
    /// especially for simulations with a large number of agents or complex environments.
    /// </summary>
    private void FixedUpdate()
    {
        if (!ValidateSimulationState())
        {
            return;
        }

        CollectAgentActions();
        StepSimulation();
        UpdateAgentPositions();

        LogDebug("TrafficManager::FixedUpdate completed successfully.");
    }

    /// <summary>
    /// Validates the current state of the traffic simulation to ensure all necessary components are properly initialized.
    /// This method performs crucial checks before allowing the simulation to proceed.
    ///
    /// The method checks the following conditions:
    /// 1. Ensures that agentInstances and agentColliders collections are not null
    /// 2. Verifies that the trafficSimulationPtr (pointer to the native simulation object) is valid
    ///
    /// Key aspects of this method:
    /// - Acts as a safeguard against running the simulation with an invalid or incomplete state
    /// - Logs specific error messages to help identify the source of initialization problems
    /// - Returns a boolean indicating whether the simulation state is valid and can proceed
    ///
    /// This method is crucial for:
    /// - Preventing null reference exceptions or invalid memory access in the simulation loop
    /// - Providing clear feedback on initialization issues for easier debugging
    /// - Ensuring the integrity of the simulation by only allowing it to run when properly set up
    ///
    /// Usage:
    /// - Typically called at the beginning of each simulation step (e.g., in FixedUpdate)
    /// - Can be used in other methods where the simulation state needs to be verified before proceeding
    ///
    /// Error messages:
    /// - "Agent instances or colliders are null": Indicates that essential collections for managing agents are not initialized
    /// - "trafficSimulationPtr is null or invalid": Suggests that the native simulation object is not properly created or linked
    ///
    /// Developers should:
    /// - Ensure proper initialization of all components before starting the simulation
    /// - Use this method's return value to guard against running the simulation in an invalid state
    /// - Investigate and resolve any reported errors before attempting to run the simulation
    ///
    /// Note: This method uses Debug.LogError, which will appear in both Editor and builds,
    /// ensuring critical initialization issues are always reported.
    /// </summary>
    /// <returns>True if the simulation state is valid and can proceed, false otherwise</returns>
    private bool ValidateSimulationState()
    {
        if (agentInstances == null || agentColliders == null)
        {
            Debug.LogError("Agent instances or colliders are null. Make sure they are properly initialized.");
            return false;
        }
        if (trafficSimulationPtr == IntPtr.Zero)
        {
            Debug.LogError("trafficSimulationPtr is null or invalid");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Collects high-level and low-level actions from all active agents in the simulation.
    /// This method prepares the action data for the next simulation step.
    ///
    /// The method performs the following actions:
    /// 1. Clears existing high-level and low-level action collections
    /// 2. Iterates through all agent instances
    /// 3. For each valid agent, adds its high-level and low-level actions to the respective collections
    ///
    /// Key aspects of this method:
    /// - Ensures fresh action data for each simulation step by clearing previous collections
    /// - Handles potential null agents gracefully by checking before accessing
    /// - Aggregates actions from all agents into centralized collections for efficient processing
    ///
    /// This method is crucial for:
    /// - Preparing the necessary input data for the simulation step
    /// - Ensuring that the simulation considers the most recent actions from all active agents
    /// - Centralizing the action collection process for better management and potential optimization
    ///
    /// Usage:
    /// - Typically called at the beginning of each simulation step, before advancing the simulation
    /// - Part of the core simulation loop, usually invoked in FixedUpdate or a similar regular update method
    ///
    /// Considerations:
    /// - The performance of this method scales with the number of agents in the simulation
    /// - Ensure that agent.highLevelActions and agent.lowLevelActions are always initialized to prevent null reference exceptions
    /// - Consider optimizing for large numbers of agents if performance becomes an issue
    ///
    /// Developers should:
    /// - Ensure that agent actions are properly set before this method is called
    /// - Be aware of the potential performance impact with a very large number of agents
    /// - Consider implementing parallel collection for significant performance gains in large-scale simulations
    ///
    /// Note: This method assumes that highLevelActions and lowLevelActions are pre-existing collections
    /// capable of storing the action data for all agents.
    /// </summary>
    private void CollectAgentActions()
    {
        highLevelActions.Clear();
        lowLevelActions.Clear();

        foreach (var agent in agentInstances.Values)
        {
            if (agent != null)
            {
                highLevelActions.Add(agent.highLevelActions);
                lowLevelActions.Add(agent.lowLevelActions);
            }
        }
    }

    /// <summary>
    /// Advances the traffic simulation by one step using the collected agent actions.
    /// This method interfaces with the native traffic simulation library to progress the simulation state.
    ///
    /// The method performs the following actions:
    /// 1. Flattens the low-level actions into a single array for native code consumption
    /// 2. Calls the native Traffic_step function with high-level and low-level actions
    /// 3. Processes the simulation result returned by the native function
    ///
    /// Key aspects of this method:
    /// - Acts as a bridge between the C# environment and the native simulation library
    /// - Transforms the collected action data into a format suitable for the native function
    /// - Assumes a specific structure for low-level actions (3 elements per agent)
    /// - Handles the raw simulation result through a separate method (ProcessSimulationResult)
    ///
    /// This method is crucial for:
    /// - Progressing the simulation state based on agent actions
    /// - Maintaining synchronization between the Unity environment and the native simulation
    /// - Enabling complex traffic behavior calculations that may be more efficient in native code
    ///
    /// Usage:
    /// - Called once per fixed update cycle, typically after collecting agent actions
    /// - Part of the core simulation loop in the TrafficManager
    ///
    /// Assumptions and constraints:
    /// - Assumes highLevelActions and lowLevelActions have been properly populated
    /// - Expects lowLevelActions to have 3 elements per agent (e.g., steering, throttle, braking)
    /// - Relies on a correctly implemented native Traffic_step function
    ///
    /// Developers should be aware of:
    /// - The potential for performance impact with large numbers of agents due to array flattening
    /// - The need for proper error handling and validation in the ProcessSimulationResult method
    /// - The importance of maintaining consistency between the C# action structure and native function expectations
    ///
    /// Note: This method uses unsafe code (via IntPtr), requiring careful memory management
    /// and potential for runtime errors if not handled correctly.
    /// </summary>
    private void StepSimulation()
    {
        Debug.Log("TrafficManger::StepSimulation started.");

        for (int i = 0; i < highLevelActions.Count; i++)
        {
            int highLevelAction = highLevelActions[i];
            Debug.Log($"High-Level Action - Decision: {highLevelAction}");

            float[] lowLevelAction = lowLevelActions[i];
            if (lowLevelAction.Length == 3) // Ensure there are exactly 3 values per action
            {
                float steering = lowLevelAction[0];
                float throttle = lowLevelAction[1];
                float braking = lowLevelAction[2];

                Debug.Log($"Low-Level Action - Steering: {steering}, Throttle: {throttle}, Braking: {braking}");
            }
        }

        // Step the simulation once for all agents with the gathered actions
        float[] flattenedLowLevelActions = lowLevelActions.SelectMany(a => a).ToArray();

        IntPtr resultPtr = Traffic_step(
            trafficSimulationPtr,
            highLevelActions.ToArray(),
            highLevelActions.Count,
            flattenedLowLevelActions,
            lowLevelActions.Count * 3 // Assuming each inner array has 3 elements (e.g., steering, throttle, braking)
        );

        ProcessSimulationResult(resultPtr);
    }

    /// <summary>
    /// Processes the result returned by the native Traffic_step function.
    /// This method handles the conversion of native memory to managed string data and performs necessary cleanup.
    ///
    /// The method performs the following actions:
    /// 1. Checks if the result pointer is valid (not null)
    /// 2. If valid, converts the native memory to a managed string
    /// 3. Logs the result string (in Unity Editor only)
    /// 4. Frees the native memory allocated for the result
    /// 5. If the pointer is null, logs an error message
    ///
    /// Key aspects of this method:
    /// - Safely handles the transition from native (unmanaged) to managed memory
    /// - Ensures proper cleanup of native memory to prevent memory leaks
    /// - Provides debug logging for monitoring simulation results
    /// - Includes error handling for null result pointers
    ///
    /// This method is crucial for:
    /// - Retrieving and interpreting the results of each simulation step
    /// - Maintaining proper memory management when interfacing with native code
    /// - Facilitating debugging and monitoring of the simulation process
    ///
    /// Usage:
    /// - Called immediately after the Traffic_step function in the StepSimulation method
    /// - Part of the core simulation loop in the TrafficManager
    ///
    /// Important considerations:
    /// - The use of Marshal.PtrToStringAnsi assumes the native string is ANSI-encoded
    /// - Debug logging is conditionally compiled and only active in the Unity Editor
    /// - Proper error handling is crucial for maintaining the stability of the simulation
    ///
    /// Developers should be aware of:
    /// - The potential for memory leaks if FreeString is not called or fails
    /// - The importance of consistent string encoding between native and managed code
    /// - The need to handle or log the result string appropriately for production builds
    ///
    /// Note: This method deals with unmanaged memory, requiring careful implementation
    /// to avoid memory-related issues. Always ensure that FreeString is called to prevent memory leaks.
    /// </summary>
    /// <param name="resultPtr">Pointer to the native memory containing the simulation result string</param>
    private void ProcessSimulationResult(IntPtr resultPtr)
    {
        if (resultPtr != IntPtr.Zero)
        {
            string result = Marshal.PtrToStringAnsi(resultPtr);

            LogDebug($"Traffic_step result: {result}");

            FreeString(resultPtr); // Free the allocated memory
        }
        else
        {
            Debug.LogError("Traffic_step returned null pointer");
        }
    }

    /// <summary>
    /// Unity's Update method, called once per frame.
    /// This method is responsible for updating the visual representation of agents in the scene.
    ///
    /// The method performs a single action:
    /// 1. Calls UpdateAgentPositions() to synchronize the visual positions of agents with their simulated positions
    ///
    /// Key aspects of this method:
    /// - Part of Unity's MonoBehaviour lifecycle, executed every frame
    /// - Focuses solely on updating visual aspects of the simulation
    /// - Separates visual updates (potentially more frequent) from physics/logic updates (FixedUpdate)
    ///
    /// This method is crucial for:
    /// - Ensuring smooth visual representation of agent movements
    /// - Maintaining synchronization between the simulation state and the rendered scene
    /// - Providing up-to-date visual feedback to the user or other game systems
    ///
    /// Usage:
    /// - Automatically called by Unity every frame
    /// - Keeps the visual aspect of the simulation updated at the frame rate
    ///
    /// Considerations:
    /// - The frequency of this method's execution depends on the frame rate, which can vary
    /// - For performance reasons, heavy computations should be avoided in Update
    /// - This method focuses on visual updates, leaving physics and logic updates to FixedUpdate
    ///
    /// Developers should be aware that:
    /// - UpdateAgentPositions() should be optimized for frequent calls
    /// - If the simulation runs at a fixed time step (in FixedUpdate), this method might cause visual interpolation
    ///   between physics steps, potentially smoothing out the movement
    /// - For very large numbers of agents, consider optimizing or batching position updates
    ///
    /// Note: While this method currently only calls UpdateAgentPositions(), it provides a clear separation
    /// of concerns between physics/logic updates and visual updates, allowing for future expansion
    /// of frame-dependent visual effects or animations if needed.
    /// </summary>
    private void Update()
    {
        Debug.Log($"Time Step: {simTimeStep}, Max Velocity: {maxSpeed}");
    }

    /// <summary>
    /// Updates the positions and orientations of all agents based on the latest simulation results.
    /// This method synchronizes the Unity representation of agents with the state of the native traffic simulation.
    ///
    /// The method performs the following key actions:
    /// 1. Retrieves updated agent positions, velocities, orientations, and previous positions from the native simulation
    /// 2. Processes each agent in the simulation, updating existing agents or spawning new ones as needed
    /// 3. Updates the C++ simulation with any changes made in Unity
    /// 4. Removes agents that are no longer present in the simulation
    ///
    /// Key aspects of this method:
    /// - Acts as the primary bridge between the native simulation state and Unity scene representation
    /// - Handles dynamic addition and removal of agents based on simulation state
    /// - Ensures consistency between Unity GameObjects and the underlying simulation data
    /// - Utilizes native interop to communicate with the C++ simulation
    ///
    /// This method is crucial for:
    /// - Maintaining visual accuracy of the simulation in the Unity scene
    /// - Handling the lifecycle of agent GameObjects (creation, update, destruction)
    /// - Synchronizing the state between the native simulation and Unity representation
    ///
    /// Implementation details:
    /// - Uses marshaling to convert between native pointers and managed types
    /// - Employs a HashSet to track updated agents for efficient removal of obsolete agents
    /// - Includes commented-out debug logs for development purposes
    /// - Contains safeguards against null or invalid data from the native simulation
    ///
    /// Performance considerations:
    /// - The method's performance scales with the number of agents in the simulation
    /// - Heavy use of interop calls may impact performance, especially with large agent counts
    /// - Consider optimizing the frequency of updates for large-scale simulations
    ///
    /// Developers should be aware that:
    /// - This method assumes the existence of various native functions (e.g., Traffic_get_agents, Vehicle_setX)
    /// - Error handling is minimal; consider adding more robust error checking for production use
    /// - The method currently updates all agents every call; consider implementing partial or prioritized updates for optimization
    /// - Commented-out debug logs can be useful for troubleshooting but may impact performance if enabled
    ///
    /// Note: This method is central to the visual representation of the simulation and should be
    /// called regularly (typically in Update or FixedUpdate) to maintain synchronization between
    /// the native simulation and Unity scene.
    /// </summary>
    public void UpdateAgentPositions()
    {
        LogDebug("TrafficManager::UpdateAgentPositions started.");

        // Get the initial state of agent
        GetSimulationData();

        HashSet<string> updatedAgents = new HashSet<string>();

        // Get all vehicles from the C++ simulation
        IntPtr vehiclesPtr = Traffic_get_agents(trafficSimulationPtr);
        int vehicleCount = VehiclePtrVector_size(vehiclesPtr);

        // Process the agent positions
        for (int i = 0; i < vehicleCount; i++)
        {
            IntPtr vehiclePtr = VehiclePtrVector_get(vehiclesPtr, i);

            if (!TryGetAgentData(i, out string agentId, out Vector3 position, out Quaternion rotation))
            {
                continue;
            }

            if (agentInstances.TryGetValue(agentId, out TrafficAgent existingAgent))
            {
                UpdateExistingAgent(existingAgent, position, rotation);
                UpdateVehicleInSimulation(vehiclePtr, position, rotation);
            }
            else
            {
                SpawnAgent(i);
            }

            updatedAgents.Add(agentId);
            UpdateColliderForExistingAgent(existingAgent.gameObject, agentId);
        }

        RemoveObsoleteAgents(updatedAgents);

        LogDebug("TrafficManager::UpdateAgentPositions completed successfully.");
    }

    private bool TryGetAgentDataPointers(int index, out string agentId, out IntPtr positionPtr, out IntPtr orientationPtr)
    {
        agentId = null;
        positionPtr = IntPtr.Zero;
        orientationPtr = IntPtr.Zero;

        IntPtr agentIdPtr = StringFloatVectorMap_get_key(agentPositionsMap, index);
        agentId = Marshal.PtrToStringAnsi(agentIdPtr);

        if (agentId == null) return false;

        positionPtr = StringFloatVectorMap_get_value(agentPositionsMap, agentIdPtr);
        orientationPtr = StringFloatVectorMap_get_value(agentOrientationsMap, agentIdPtr);

        return positionPtr != IntPtr.Zero && orientationPtr != IntPtr.Zero;
    }

    /// <summary>
    /// Attempts to retrieve agent data from the native simulation.
    ///
    /// This method:
    /// 1. Extracts agent ID, position, and rotation from native data structures
    /// 2. Converts native data to managed types (string, Vector3, Quaternion)
    ///
    /// Key aspects:
    /// - Uses native interop to access simulation data
    /// - Implements a try-pattern for safe data retrieval
    /// - Handles potential null or invalid data
    ///
    /// Usage:
    /// - Call when needing to synchronize agent data between native and managed code
    ///
    /// Note: Ensure proper error handling when using this method, as it may fail due to invalid native data.
    /// </summary>
    /// <param name="index">Index of the agent in the native data structure</param>
    /// <param name="agentId">Output parameter for the agent's ID</param>
    /// <param name="position">Output parameter for the agent's position</param>
    /// <param name="rotation">Output parameter for the agent's rotation</param>
    /// <returns>True if data was successfully retrieved, false otherwise</returns>
    private bool TryGetAgentData(int index, out string agentId, out Vector3 position, out Quaternion rotation)
    {
        position = default;
        rotation = default;

        if (!TryGetAgentDataPointers(index, out agentId, out IntPtr positionPtr, out IntPtr orientationPtr))
        {
            Debug.LogWarning($"Failed to get position or orientation for agent {agentId}");
            return false;
        }

        position = GetVector3FromFloatVector(positionPtr);
        rotation = GetQuaternionFromFloatVector(orientationPtr);

        // Assuming currentSteeringAngle is updated elsewhere (rotationSpeed can be set later to yaw rate)
        //Quaternion targetRotation = Quaternion.Euler(0, 0.64f * Mathf.Rad2Deg, 0);
        //transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        return true;
    }

    /// <summary>
    /// Updates the position of a vehicle in the native simulation.
    ///
    /// This method:
    /// 1. Sets the X, Y, and Z coordinates of a vehicle in the C++ simulation
    ///
    /// Key aspects:
    /// - Bridges Unity's Vector3 position to native C++ representation
    /// - Uses native function calls to update vehicle position
    ///
    /// Usage:
    /// - Call when synchronizing Unity GameObject positions with the native simulation
    ///
    /// Note: Ensure the vehiclePtr is valid before calling to avoid crashes.
    /// </summary>
    /// <param name="vehiclePtr">Pointer to the vehicle in the native simulation</param>
    /// <param name="position">New position of the vehicle in Unity coordinates</param>
    private void UpdateVehicleInSimulation(IntPtr vehiclePtr, Vector3 position, Quaternion rotation)
    {
        // Get vehicle pointer from the C++ simulation
        Vehicle_setX(vehiclePtr, position.x);
        Vehicle_setY(vehiclePtr, position.y);
        Vehicle_setZ(vehiclePtr, position.z);

        // Update the vehicle's rotation in the C++ simulation
        Vehicle_setSteering(vehiclePtr, rotation.y);
        Vehicle_setYaw(vehiclePtr, lowLevelActions[0][0]);
    }

    /// <summary>
    /// Removes agents that are no longer present in the simulation.
    ///
    /// This method:
    /// 1. Identifies agents not in the updated set
    /// 2. Destroys their GameObjects
    /// 3. Removes them from agent collections
    ///
    /// Key aspects:
    /// - Maintains consistency between simulation state and Unity scene
    /// - Efficiently uses LINQ for identifying obsolete agents
    /// - Safely removes agents from multiple collections
    ///
    /// Usage:
    /// - Call after updating agent positions to clean up obsolete agents
    ///
    /// Note: Ensure this is called at appropriate intervals to prevent
    /// accumulation of inactive agents and maintain performance.
    /// </summary>
    /// <param name="updatedAgents">Set of agent IDs that are still active in the simulation</param>
    private void RemoveObsoleteAgents(HashSet<string> updatedAgents)
    {
        var agentsToRemove = agentInstances.Keys.Where(id => !updatedAgents.Contains(id)).ToList();

        foreach (string agentId in agentsToRemove)
        {
            if (agentInstances.TryGetValue(agentId, out TrafficAgent agent))
            {
                Destroy(agent.gameObject);
                agentInstances.Remove(agentId);
                agentColliders.Remove(agentId);
            }
        }
    }

    /// <summary>
    /// Logs a debug message to the Unity console, but only when running in the Unity Editor.
    /// This method provides a centralized and controlled way to output debug information.
    ///
    /// The method performs the following action:
    /// 1. If running in the Unity Editor, logs the provided message using Unity's Debug.Log
    ///
    /// Key aspects of this method:
    /// - Uses conditional compilation to ensure debug logs only appear in the Unity Editor
    /// - Centralizes debug logging, making it easier to manage and control debug output
    /// - Helps prevent debug logs from affecting performance in builds
    ///
    /// This method is useful for:
    /// - Debugging and development purposes within the Unity Editor
    /// - Providing insights into the simulation's behavior during development
    /// - Allowing for easy enabling/disabling of debug logs across the entire project
    ///
    /// Usage:
    /// - Call this method instead of Debug.Log directly throughout the codebase
    /// - Ideal for temporary debugging or for logging non-critical information
    ///
    /// Considerations:
    /// - Debug messages will not appear in builds, ensuring clean release versions
    /// - Using this method allows for easy future expansion of logging functionality
    /// - Consider adding log levels or additional conditionals if more complex logging is needed
    ///
    /// Developers should be aware that:
    /// - Overuse of logging can impact editor performance during play mode
    /// - This method should not be used for logging critical errors that need to be visible in builds
    /// - It's a good practice to remove or comment out unnecessary debug logs before final release
    ///
    /// Note: While this method provides a convenient way to add debug logs, it's important to use
    /// logging judiciously to maintain code readability and performance, especially in frequently
    /// called methods or performance-critical sections of code.
    /// </summary>
    /// <param name="message">The debug message to be logged</param>
    private void LogDebug(string message)
    {
        //#if UNITY_EDITOR
        Debug.Log(message);
        //#endif
    }

    private void MaxEpisodeSteps(float newValue)
    {
        LogDebug("TrafficManager::MaxEpisodeSteps started.");

        MaxSteps = Mathf.RoundToInt(newValue);

        LogDebug("TrafficManager::MaxEpisodeSteps completely successfully.");
    }

    /// <summary>
    /// Restarts the traffic simulation, resetting it to its initial state.
    /// This method performs a comprehensive reset of the simulation environment,
    /// recreating the simulation state without fully disposing of the TrafficManager itself.
    ///
    /// The method performs the following sequence of actions:
    /// 1. Cleans up the existing simulation state
    /// 2. Recreates the simulation environment
    /// 3. Reinitializes simulation data
    /// 4. Initializes agents
    /// 5. Validates the agent count
    ///
    /// Key aspects of this method:
    /// - Provides a way to reset the simulation to its starting state during runtime
    /// - Utilizes a try-catch block to handle potential errors during the restart process
    /// - Includes debug logging to track the restart process (some logs are Unity Editor-only)
    /// - Calls several helper methods to modularize the restart process
    ///
    /// This method is crucial for:
    /// - Allowing the simulation to be reset without restarting the entire application
    /// - Facilitating testing and debugging of different simulation scenarios
    /// - Providing a clean slate for new simulation episodes or configurations
    ///
    /// Usage:
    /// - Can be called manually or triggered by game events to reset the simulation
    /// - Useful for scenarios where the simulation needs to be restarted with new parameters
    ///
    /// Error Handling:
    /// - Utilizes a try-catch block to capture and handle any exceptions during the restart process
    /// - Calls HandleRestartError to manage any errors that occur
    ///
    /// Developers should be aware that:
    /// - This method performs a deep reset of the simulation state
    /// - It may be computationally expensive, especially for large simulations
    /// - Proper error handling is crucial to maintain system stability during restarts
    /// - The method assumes the existence of several helper methods (CleanUpSimulation, RecreateSimulation, etc.)
    ///
    /// Note: The use of #if UNITY_EDITOR directives indicates that some debug logging
    /// is only active in the Unity Editor, not in builds. This helps in development
    /// without affecting release performance.
    /// </summary>
    public void Reset()
    {
        // Reset the simulation state, this method should prepare the environment
        // for a new episode without fully cleaning up or disposing of objects
        LogDebug("TrafficManager::RestartSimulation stared");

        try
        {
            // Clean up existing simulation
            CleanUpSimulation();
            CreateSimulation();
            GetSimulationData();

            // Reinitialize agents
            InitializeAgents();

            Debug.Log("Traffic simulation restarted successfully");
        }
        catch (Exception e)
        {
            HandleRestartError(e);
        }

        LogDebug("TrafficManager::RestartSimulation completed successfully.");
    }

    /// <summary>
    /// Creates the traffic simulation with the current initial agent count and seed.
    /// This method is responsible for instantiating a new native traffic simulation object.
    ///
    /// The method performs the following actions:
    /// 1. Calls the native Traffic_create function with the current initialAgentCount and seed
    /// 2. Stores the returned pointer to the new simulation object
    /// 3. Validates the returned pointer to ensure successful creation
    ///
    /// Key aspects of this method:
    /// - Interfaces with native code to create a new simulation instance
    /// - Uses the current initialAgentCount and seed values for configuration
    /// - Throws an exception if the simulation creation fails
    ///
    /// This method is crucial for:
    /// - Reinitializing the simulation with potentially new parameters
    /// - Ensuring a clean slate for the simulation state
    /// - Managing the lifecycle of the native simulation object
    ///
    /// Usage:
    /// - Typically called as part of the RestartSimulation process
    /// - May be used when significant changes to the simulation parameters require a fresh start
    ///
    /// Error Handling:
    /// - Throws an InvalidOperationException if the native function returns a null pointer
    /// - This exception should be caught and handled by the calling method (e.g., RestartSimulation)
    ///
    /// Developers should be aware that:
    /// - This method deals with unmanaged memory and requires careful handling
    /// - The previous simulation instance should be properly disposed of before calling this method
    /// - The initialAgentCount and seed values should be properly set before calling this method
    /// - This operation may be resource-intensive, especially for large simulations
    ///
    /// Note: The use of IntPtr indicates interaction with native code, which requires
    /// proper memory management and error checking to prevent memory leaks and crashes.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the native function fails to create a new simulation</exception>
    private IntPtr CreateSimulation()
    {
        // Recreate the simulation with the new agent count
        trafficSimulationPtr = Traffic_create(initialAgentCount, seed);

        if (trafficSimulationPtr == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create new traffic simulation.");
        }

        return trafficSimulationPtr;
    }

    /// <summary>
    /// Reinitializes the simulation data by fetching updated information from the native simulation object.
    /// This method refreshes the managed representations of agent positions, velocities, orientations, and previous positions.
    ///
    /// The method performs the following actions:
    /// 1. Retrieves updated agent positions from the native simulation
    /// 2. Fetches current agent velocities
    /// 3. Obtains the latest agent orientations
    /// 4. Acquires the previous positions of agents
    ///
    /// Key aspects of this method:
    /// - Serves as a bridge between the native simulation state and managed C# data structures
    /// - Ensures that all managed data is synchronized with the current state of the native simulation
    /// - Utilizes native function calls to retrieve data from the simulation object
    ///
    /// This method is crucial for:
    /// - Maintaining consistency between the native simulation state and the Unity representation
    /// - Preparing the managed environment for a new simulation cycle or after a restart
    /// - Enabling accurate rendering and behavior of agents in the Unity scene
    ///
    /// Usage:
    /// - Typically called as part of the RestartSimulation process
    /// - May be used after any operation that significantly alters the simulation state
    ///
    /// Considerations:
    /// - Assumes that the trafficSimulationPtr is valid and points to an active simulation object
    /// - The native functions (Traffic_get_*) are expected to return valid data structures
    /// - This operation may involve significant data transfer between native and managed code
    ///
    /// Developers should be aware that:
    /// - This method does not perform error checking on the returned data; ensure native calls are reliable
    /// - The performance of this method scales with the number of agents and the complexity of the data structures
    /// - Proper memory management is crucial, especially if the returned data structures need to be manually freed
    /// - This method should be called at appropriate times to ensure data consistency across the entire system
    ///
    /// Note: The method relies on several native function calls, indicating a tight coupling with
    /// the underlying C++ simulation library. Any changes to the native interface should be
    /// reflected here to maintain synchronization.
    /// </summary>
    private void GetSimulationData()
    {
        // Reinitialize agent positions, velocities, etc.
        agentPositionsMap = Traffic_get_agent_positions(trafficSimulationPtr);
        agentVelocitiesMap = Traffic_get_agent_velocities(trafficSimulationPtr);
        agentOrientationsMap = Traffic_get_agent_orientations(trafficSimulationPtr);
        agentPreviousPositionsMap = Traffic_get_previous_positions(trafficSimulationPtr);
    }

    /// <summary>
    /// Validates that the number of agent instances matches the expected initial agent count.
    /// This method serves as a consistency check to ensure the simulation is correctly initialized.
    ///
    /// The method performs the following action:
    /// 1. Compares the count of agent instances in agentInstances with the initialAgentCount
    /// 2. Logs a warning if there's a mismatch between these values
    ///
    /// Key aspects of this method:
    /// - Acts as a safeguard against inconsistencies in agent initialization
    /// - Provides immediate feedback if the actual agent count doesn't match the expected count
    /// - Uses Unity's debug logging system to report discrepancies
    ///
    /// This method is crucial for:
    /// - Detecting potential errors in the agent initialization process
    /// - Ensuring the integrity of the simulation state
    /// - Facilitating debugging of agent creation and management issues
    ///
    /// Usage:
    /// - Typically called after agent initialization or as part of the simulation restart process
    /// - Can be used as a sanity check at various points in the simulation lifecycle
    ///
    /// Considerations:
    /// - The warning is logged using Debug.LogWarning, which is visible in both Editor and builds
    /// - This method does not correct the mismatch; it only reports it
    /// - Regular validation can help catch issues early in the development process
    ///
    /// Developers should be aware that:
    /// - A mismatch could indicate problems in agent creation, destruction, or management
    /// - This validation is particularly important after operations that modify the agent count
    /// - Consistent mismatches may point to underlying issues in the simulation logic
    /// - While this method reports issues, resolution typically requires investigation of agent lifecycle management
    ///
    /// Note: This method assumes that initialAgentCount represents the desired number of agents,
    /// and agentInstances accurately reflects the current state of agents in the simulation.
    /// Any discrepancy between these should be treated as a potential issue requiring attention.
    /// </summary>
    private void ValidateAgentCount()
    {
        if (agentInstances.Count != initialAgentCount)
        {
            Debug.LogWarning($"Mismatch between initialAgentCount ({initialAgentCount}) and actual agent count ({agentInstances.Count})");
        }
    }

    /// <summary>
    /// Handles errors that occur during the simulation restart process.
    /// This method provides a centralized way to manage exceptions thrown during RestartSimulation.
    ///
    /// The method performs the following actions:
    /// 1. Logs the error message and stack trace of the caught exception
    /// 2. Disables the TrafficManager component to prevent further execution
    ///
    /// Key aspects of this method:
    /// - Acts as an error handler specifically for the RestartSimulation process
    /// - Utilizes Unity's debug logging system to report detailed error information
    /// - Takes a preventive measure by disabling the TrafficManager component
    ///
    /// This method is crucial for:
    /// - Providing detailed error information to aid in debugging restart issues
    /// - Preventing the simulation from continuing in an potentially invalid state
    /// - Centralizing error handling logic for the restart process
    ///
    /// Usage:
    /// - Called from within a catch block in the RestartSimulation method
    /// - Serves as the last line of defense when critical errors occur during restart
    ///
    /// Error Reporting:
    /// - Uses Debug.LogError to ensure high visibility of the error in the Unity console
    /// - Includes both the exception message and stack trace for comprehensive error information
    ///
    /// Developers should be aware that:
    /// - This method will effectively stop the simulation by disabling the TrafficManager
    /// - The error log provides crucial information for diagnosing and fixing restart-related issues
    /// - After this method is called, manual intervention may be required to re-enable the simulation
    /// - The disabling of the component is a safety measure to prevent cascading errors
    ///
    /// Note: While this method provides a way to gracefully handle restart errors, it's important
    /// to investigate and address the root cause of any errors that lead to its invocation.
    /// Regular occurrence of restart errors may indicate underlying issues in the simulation logic or setup.
    /// </summary>
    /// <param name="e">The exception that was caught during the restart process</param>
    private void HandleRestartError(Exception e)
    {
        Debug.LogError($"Error in RestartSimulation: {e.Message}\n{e.StackTrace}");
        enabled = false;
    }

    /// <summary>
    /// Performs a comprehensive cleanup of the traffic simulation environment.
    /// This method is responsible for safely disposing of all simulation-related resources and resetting the simulation state.
    ///
    /// The method performs the following sequence of actions:
    /// 1. Checks if cleanup has already been performed to avoid redundant operations
    /// 2. Destroys all agent GameObjects and clears agent collections
    /// 3. Destroys the native traffic simulation object and nullifies its pointer
    /// 4. Cleans up various map references used in the simulation
    /// 5. Unregisters the float properties side channel
    /// 6. Marks the cleanup as completed
    ///
    /// Key aspects of this method:
    /// - Ensures proper disposal of both managed (Unity) and unmanaged (native) resources
    /// - Prevents memory leaks by destroying GameObjects and freeing native memory
    /// - Resets all major data structures used in the simulation
    /// - Includes safeguards against multiple cleanups and null references
    /// - Uses conditional compilation for debug logging in the Unity Editor
    ///
    /// This method is crucial for:
    /// - Properly shutting down the simulation and freeing resources
    /// - Preparing the system for a new simulation instance or application quit
    /// - Preventing resource leaks and ensuring clean state transitions
    ///
    /// Usage:
    /// - Called during simulation restart, application quit, or when the TrafficManager is being destroyed
    /// - Can be used to reset the simulation state without recreating the TrafficManager itself
    ///
    /// Cleanup Details:
    /// - Agent GameObjects are destroyed using Unity's Destroy method
    /// - Native simulation object is destroyed using a custom Traffic_destroy function
    /// - Map references are cleaned up using a separate CleanUpMap method
    /// - Side channel is unregistered to ensure proper communication shutdown
    ///
    /// Developers should be aware that:
    /// - This method is designed to be idempotent (safe to call multiple times)
    /// - It handles both Unity-managed objects and native pointers, requiring careful memory management
    /// - Debug logs are only active in the Unity Editor, helping with development without affecting builds
    /// - The hasCleanedUp flag prevents redundant cleanup operations
    ///
    /// Note: Proper and timely invocation of this method is critical for maintaining system integrity,
    /// especially when dealing with native resources and Unity object lifecycles. Ensure this method
    /// is called appropriately in all scenarios where the simulation needs to be reset or shut down.
    /// </summary>
    private void CleanUpSimulation()
    {
        LogDebug("TrafficManager::CleanUpSimulation strated.");

        if (hasCleanedUp) return;

        // Clean up existing agents
        foreach (var agentInstance in agentInstances.Values)
        {
            if (agentInstance != null && agentInstance.gameObject != null)
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

        // Unregister the channel
        if (floatPropertiesChannel != null)
        {
            // Unregister the channel when the TrafficManager is destroyed
            SideChannelManager.UnregisterSideChannel(floatPropertiesChannel);
            floatPropertiesChannel = null; // NEW
        }

        if(sideChannel != null)
        {
            SideChannelManager.UnregisterSideChannel(sideChannel);
            sideChannel = null;
        }


        LogDebug("TrafficManager::CleanUpSimulation completely successfully.");

        hasCleanedUp = true;
    }

    /// <summary>
    /// Called when the TrafficManager script is enabled.
    /// This method is part of Unity's MonoBehaviour lifecycle and is automatically invoked
    /// when the script instance becomes enabled and active.
    ///
    /// Currently, this method contains only commented-out debug logs, which can be useful for:
    /// - Tracking when the TrafficManager is enabled during runtime or in the editor
    /// - Serving as a placeholder for future initialization code that needs to run when the script is enabled
    /// - Debugging the enable/disable cycle of the TrafficManager
    ///
    /// The debug logs, when uncommented, would print messages at the start and end of the method execution,
    /// allowing for precise tracking of when the TrafficManager becomes active.
    ///
    /// Note: The debug logs are wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring they only appear in the Unity Editor and not in builds.
    ///
    /// This method can be extended to include any setup or initialization code that should run
    /// every time the TrafficManager is enabled, not just on initial startup.
    /// </summary>
    private void OnEnable()
    {
        LogDebug("TrafficManager::OnEnable started.");

        LogDebug("TrafficManager::OnEnable completely successfully.");
    }

    /// <summary>
    /// Called when the TrafficManager script becomes disabled or inactive.
    /// This method is part of Unity's MonoBehaviour lifecycle and is automatically invoked
    /// when the script instance is disabled or becomes inactive.
    ///
    /// Currently, this method contains only commented-out debug logs, which can be useful for:
    /// - Tracking when the TrafficManager is disabled during runtime or in the editor
    /// - Serving as a placeholder for future cleanup or state-saving code that needs to run when the script is disabled
    /// - Debugging the enable/disable cycle of the TrafficManager
    ///
    /// The debug logs, when uncommented, would print messages at the start and end of the method execution,
    /// allowing for precise tracking of when the TrafficManager becomes inactive.
    ///
    /// Note: The debug logs are wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring they only appear in the Unity Editor and not in builds.
    ///
    /// This method can be extended to include any cleanup, state-saving, or resource-releasing code
    /// that should run every time the TrafficManager is disabled. This could include:
    /// - Pausing or stopping ongoing traffic simulations
    /// - Saving the current state of the traffic system
    /// - Releasing any resources that are not needed while the script is inactive
    ///
    /// Proper implementation of OnDisable() can help manage resources efficiently and ensure
    /// smooth transitions when the TrafficManager is toggled on and off during runtime.
    /// </summary>
    private void OnDisable()
    {
        LogDebug("TrafficManager::OnDisable started.");


        LogDebug("TrafficManager::OnDisable completely successfully.");
    }

    /// <summary>
    /// Called when the TrafficManager script or its GameObject is being destroyed.
    /// This method is part of Unity's MonoBehaviour lifecycle and is automatically invoked
    /// when the script instance is being destroyed, typically when the scene or game is ending.
    ///
    /// The method performs the following actions:
    /// 1. Calls CleanUpSimulation() to perform necessary cleanup operations
    /// 2. Includes commented-out debug logs for tracking the start and end of the destruction process
    ///
    /// Key aspects of this method:
    /// - It ensures proper cleanup of resources and state when the TrafficManager is being destroyed
    /// - The CleanUpSimulation() call is crucial for releasing any managed or unmanaged resources,
    ///   stopping ongoing processes, or performing any final data saving or logging
    /// - Debug logs (when uncommented) provide visibility into the destruction process, which can be
    ///   helpful for debugging or tracking the lifecycle of the TrafficManager
    ///
    /// Note: The debug logs are wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring they only appear in the Unity Editor and not in builds.
    ///
    /// Proper implementation of OnDestroy() is essential for:
    /// - Preventing memory leaks
    /// - Ensuring clean shutdown of the traffic simulation system
    /// - Properly releasing any external resources or connections
    /// - Maintaining the overall stability and performance of the application
    ///
    /// Developers should ensure that CleanUpSimulation() thoroughly handles all necessary cleanup tasks.
    /// </summary>
    private void OnDestroy()
    {
        LogDebug("TrafficManager::OnDestroy started.");

        CleanUpSimulation();

        Traffic_destroy(trafficSimulationPtr); // NEW

        LogDebug("TrafficManager::OnDestroy completely successfully.");
    }

    /// <summary>
    /// Cleans up and destroys a StringFloatVectorMap object referenced by an IntPtr.
    /// This method is responsible for properly disposing of native resources allocated for the map.
    ///
    /// The method performs the following actions:
    /// 1. Checks if the provided IntPtr is not null (Zero)
    /// 2. If valid, calls the native StringFloatVectorMap_destroy function to free the associated memory
    /// 3. Sets the IntPtr to Zero after destruction to prevent potential use-after-free issues
    ///
    /// Key aspects of this method:
    /// - It's crucial for preventing memory leaks when working with native (C++) objects in Unity
    /// - The method uses a ref parameter to ensure the IntPtr is nullified after cleanup
    /// - Commented-out debug logs are included for potential debugging and tracking of the cleanup process
    ///
    /// Note: The debug logs are wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring they only appear in the Unity Editor and not in builds.
    ///
    /// This method is typically called during the cleanup phase of the TrafficManager,
    /// such as in the OnDestroy() method or when reinitializing the simulation.
    ///
    /// Proper use of this method is essential for:
    /// - Efficient management of native memory resources
    /// - Preventing memory leaks in long-running simulations or when frequently reinitializing the traffic system
    /// - Ensuring clean shutdown of the traffic simulation system
    ///
    /// Developers should ensure this method is called for all StringFloatVectorMap objects
    /// that are no longer needed to maintain the overall stability and performance of the application.
    /// </summary>
    /// <param name="map">A reference to the IntPtr representing the StringFloatVectorMap to be destroyed</param>
    private void CleanUpMap(ref IntPtr map)
    {
        LogDebug("TrafficManager::CleanUpMap started");
        if (map != IntPtr.Zero)
        {
            StringFloatVectorMap_destroy(map);
            map = IntPtr.Zero;
        }

        LogDebug("TrafficManager::CleanUpMap completely successfully.");
    }

    /// <summary>
    /// Called when the application is about to quit.
    /// This method is part of Unity's MonoBehaviour lifecycle and is automatically invoked
    /// when the application is closing or when the scene is being unloaded.
    ///
    /// The method performs the following actions:
    /// 1. Calls CleanUpSimulation() to perform necessary cleanup operations
    /// 2. Includes commented-out debug logs for tracking the start and end of the quit process
    ///
    /// Key aspects of this method:
    /// - It ensures proper cleanup of resources and state when the application is closing
    /// - The CleanUpSimulation() call is crucial for releasing any managed or unmanaged resources,
    ///   stopping ongoing processes, or performing any final data saving or logging
    /// - This method provides a last chance to perform cleanup before the application exits
    /// - It's particularly important for ensuring that native resources (like those used in traffic simulation)
    ///   are properly disposed of to prevent memory leaks or system resource issues
    ///
    /// Note: The debug logs are wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring they only appear in the Unity Editor and not in builds.
    ///
    /// Proper implementation of OnApplicationQuit() is essential for:
    /// - Ensuring a clean shutdown of the traffic simulation system
    /// - Preventing resource leaks that could affect system performance after the application closes
    /// - Saving any critical data or state before the application exits
    /// - Maintaining the overall stability and integrity of the application and the system it runs on
    ///
    /// Developers should ensure that CleanUpSimulation() thoroughly handles all necessary cleanup tasks,
    /// particularly those involving native or unmanaged resources.
    /// </summary>
    private void OnApplicationQuit()
    {
        LogDebug("TrafficManager::OnApplicationQuit started.");

        CleanUpSimulation();

        LogDebug("TrafficManager::OnApplicationQuit completely successfully.");
    }

    /// <summary>
    /// Implements the IDisposable pattern to clean up resources used by the TrafficManager.
    /// This method ensures that both managed and unmanaged resources are properly released.
    ///
    /// The method performs the following actions:
    /// 1. Checks if the object has already been disposed to prevent multiple disposals
    /// 2. Calls CleanUpSimulation() to perform necessary cleanup operations
    /// 3. Sets the isDisposed flag to true to mark the object as disposed
    /// 4. Calls GC.SuppressFinalize(this) to prevent the finalizer from running on this object
    /// 5. Includes commented-out debug logs for tracking the disposal process
    ///
    /// Key aspects of this method:
    /// - It's part of the IDisposable pattern, allowing for deterministic cleanup of resources
    /// - The check for isDisposed ensures that resources are not cleaned up multiple times
    /// - CleanUpSimulation() is called to handle the actual resource release and cleanup
    /// - GC.SuppressFinalize(this) is called to improve performance by informing the garbage collector
    ///   that it doesn't need to call the finalizer on this object
    /// - This method provides a way to manually trigger cleanup when the TrafficManager is no longer needed
    ///
    /// Note: The debug logs are wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring they only appear in the Unity Editor and not in builds.
    ///
    /// Proper implementation of Dispose() is essential for:
    /// - Ensuring timely and deterministic cleanup of resources
    /// - Preventing resource leaks, especially for unmanaged resources
    /// - Allowing the TrafficManager to be used in 'using' statements for automatic resource management
    /// - Maintaining good coding practices and resource management in C#
    ///
    /// Developers should ensure that this method is called when the TrafficManager is no longer needed,
    /// or use it in conjunction with the 'using' statement for automatic disposal.
    /// </summary>
    public void Dispose()
    {
        LogDebug("TrafficManager::Dispose started");

        if (!isDisposed)
        {
            CleanUpSimulation();
            isDisposed = true;
            GC.SuppressFinalize(this);
        }

        LogDebug("TrafficManager::Dispose completely successfully.");
    }

    /// <summary>
    /// Finalizer for the TrafficManager class.
    /// This method serves as a safety net for cleaning up resources if the Dispose method is not called explicitly.
    ///
    /// The finalizer performs the following actions:
    /// 1. Calls the Dispose() method to ensure resources are cleaned up
    /// 2. Includes commented-out debug logs for tracking the finalization process
    ///
    /// Key aspects of this finalizer:
    /// - It's part of the dispose pattern implementation, working in conjunction with the Dispose() method
    /// - Provides a last resort for resource cleanup if the object is not properly disposed of
    /// - Calls Dispose() to ensure consistent cleanup logic between manual and automatic disposal
    /// - Should ideally never be called if the object is properly disposed of manually
    ///
    /// Note: The debug logs are wrapped in UNITY_EDITOR conditional compilation directives,
    /// ensuring they only appear in the Unity Editor and not in builds.
    ///
    /// Important considerations:
    /// - Finalizers are not deterministic and may run at any time after the object becomes eligible for garbage collection
    /// - Relying on finalizers for cleanup can lead to delayed resource release and potential resource leaks
    /// - The presence of a finalizer can impact performance as it prevents the object from being collected in Gen0
    /// - It's always preferable to call Dispose() explicitly rather than relying on the finalizer
    ///
    /// Developers should:
    /// - Ensure that Dispose() is called explicitly when the TrafficManager is no longer needed
    /// - Use 'using' statements or try-finally blocks to guarantee proper disposal
    /// - Be aware that the finalizer exists as a safeguard, but should not be relied upon for timely cleanup
    ///
    /// The finalizer's primary role is to prevent resource leaks in cases where manual disposal is overlooked,
    /// but it should be considered a last resort for resource cleanup.
    /// </summary>
    ~TrafficManager()
    {
        LogDebug("TrafficManager::~TrafficManager started.");

        Dispose();

        LogDebug("TrafficManager::~TrafficManager completely successfully.");
    }

    /// <summary>
    /// Adds a RayPerceptionSensorComponent3D to the specified agent GameObject.
    /// Configures the sensor for detecting specified tags, and allows sensor position adjustment relative to the agent.
    /// </summary>
    /// <param name="agent">The GameObject representing the traffic agent to which the ray sensor will be added.</param>
    void AddRaySensorToAgent(GameObject agent)
    {
        Debug.Log($"Adding RaySensor to agent: {agent.name}");

        // If you need to adjust the sensor's position relative to the car:
        GameObject sensorObject = new GameObject("RaySensor");
        sensorObject.transform.SetParent(agent.transform);
        sensorObject.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f); // Raycasts emit from collider box center-of-geometry

        // Add and configure the RayPerceptionSensorComponent3D
        //RayPerceptionSensorComponent3D raySensor = sensorObject.AddComponent<RayPerceptionSensorComponent3D>();
        //raySensor = sensorObject.AddComponent<RayPerceptionSensorComponent3D>();
        raySensor = sensorObject.AddComponent<RayPerceptionSensorComponent3D>();

        // Configure the sensor
        raySensor.SensorName = $"{agent.name}_RaySensor";
        raySensor.DetectableTags = new List<string> { "RoadBoundary", "TrafficAgent"};
        raySensor.RaysPerDirection = 15;
        raySensor.MaxRayDegrees = 180;
        raySensor.SphereCastRadius = 0.5f;
        raySensor.RayLength = 100f;
        raySensor.ObservationStacks = 1;
        raySensor.StartVerticalOffset = 2.5f;

        Debug.Log($"Finished adding RaySensor to agent: {agent.name}");
        Debug.Log($"RaySensor configuration: RaysPerDirection={raySensor.RaysPerDirection}, " +
                  $"MaxRayDegrees={raySensor.MaxRayDegrees}, SphereCastRadius={raySensor.SphereCastRadius}, " +
                  $"RayLength={raySensor.RayLength}");
    }
}

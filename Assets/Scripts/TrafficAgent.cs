using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;



//private TrafficManager trafficManager;

public class TrafficAgent : Agent
{
    // Constants
    private const string DllName = "ReplicantDriveSim";

    // DllImport Declarations
    [DllImport(DllName)]
    public static extern int FloatVector_size(IntPtr vector);

    [DllImport(DllName)]
    public static extern float FloatVector_get(IntPtr vector, int index);

    [DllImport(DllName)]
    public static extern void FloatVector_destroy(IntPtr vector);

    [DllImport(DllName)]
    public static extern int StringFloatVectorMap_size(IntPtr map);

    [DllImport(DllName)]
    public static extern IntPtr StringFloatVectorMap_get_key(IntPtr map, int index);

    [DllImport(DllName)]
    public static extern IntPtr StringFloatVectorMap_get_value(IntPtr map, IntPtr key);

    [DllImport(DllName)]
    public static extern void StringFloatVectorMap_destroy(IntPtr map);

    [DllImport(DllName)]
    private static extern IntPtr Traffic_create(int num_agents, uint seed);

    [DllImport(DllName)]
    private static extern void Traffic_destroy(IntPtr traffic);

    [DllImport(DllName)]
    private static extern void Traffic_step(IntPtr traffic, int[] high_level_actions, float[] low_level_actions);

    [DllImport(DllName)]
    private static extern IntPtr Traffic_get_agent_positions(IntPtr traffic);

    [DllImport(DllName)]
    private static extern IntPtr Traffic_get_agent_velocities(IntPtr traffic);

    [DllImport(DllName)]
    private static extern IntPtr Traffic_get_agent_orientations(IntPtr traffic);

    [DllImport(DllName)]
    private static extern IntPtr Traffic_get_previous_positions(IntPtr traffic);

    // Private Fields
    private IntPtr trafficSimulationPtr;
    private IntPtr agentPositionsMap;
    private IntPtr agentVelocitiesMap;
    private IntPtr agentOrientationsMap;
    private IntPtr agentPreviousPositionsMap;

    private int[] highLevelActions;
    private float[] lowLevelActions;

    private Dictionary<string, GameObject> agentInstances = new Dictionary<string, GameObject>();
    private Dictionary<string, Collider> agentColliders = new Dictionary<string, Collider>();

    // Serialized Fields
    [SerializeField]
    private int numberOfRays = 15;

    [SerializeField]
    private float rayLength = 20f;

    [SerializeField]
    private float raycastAngle = 90f;

    [SerializeField]
    private Color hitColor = Color.red;

    [SerializeField]
    private Color missColor = Color.green;

    // Properties
    private float AngleStep;

    private bool hasNewActions = false;

    // Public Fields
    public GameObject agentPrefab;  // Reference to the agent prefab (e.g., a car model such as Mercedes-Benz AMG GT-R)

    // Add this line at the class level
    private TrafficManager trafficManager;

    private void Awake()
    {
        AngleStep = raycastAngle / (numberOfRays - 1);

        // Clear existing agents and colliders
        if (agentInstances != null)
        {
            foreach (var agent in agentInstances.Values)
            {
                if (agent != null)
                {
                    Destroy(agent);
                }
            }
            agentInstances.Clear();
        }
        else
        {
            agentInstances = new Dictionary<string, GameObject>();
        }

        if (agentColliders != null)
        {
            agentColliders.Clear();
        }
        else
        {
            agentColliders = new Dictionary<string, Collider>();
        }

        Debug.Log("=== Awake End ===");
    }

    // Initialize - ML-Agents specific initialization
    // Initialize the agent and the traffic simulation
    public override void Initialize()
    {
        Debug.Log("=== Initialize ===");

        base.Initialize();

        // Find the TrafficManager in the scene
        trafficManager = FindObjectOfType<TrafficManager>();
        if (trafficManager == null)
        {
            Debug.LogError("TrafficManager not found in the scene. Please add a TrafficManager to your scene.");
        }

        // Assuming you have a reference to the Traffic_create and Traffic_destroy functions from the C API
        trafficSimulationPtr = Traffic_create(2, 12345); // Create simulation with 2 agents and seed 12345

        // Get the initial agent positions and create agent instances
        UpdateAgentPositions();
        Debug.Log("=== Initialize End ===");
    }

    // Add this method to allow TrafficManager to set itself
    public void SetTrafficManager(TrafficManager manager)
    {
        trafficManager = manager;
    }

    private void FixedUpdate()
    {
        Debug.Log("^^^ FixedUpdate ^^^");

        // Optionally, if you step the simulation every fixed update
        // int[] highLevelActions = ...;
        // float[] lowLevelActions = ...;
        //Traffic_step(trafficSimulationPtr, highLevelActions, lowLevelActions);

        // Update agent positions
        //UpdateAgentPositions();

        if (hasNewActions)
        {
            // Step the traffic simulation with the most recent actions
            Traffic_step(trafficSimulationPtr, highLevelActions, lowLevelActions);
            hasNewActions = false;

            // Update agent positions after stepping the simulation
            UpdateAgentPositions();

            // Request a new decision for the next step
            RequestDecision();
        }

    }

    // Collect observations from the environment
    public override void CollectObservations(VectorSensor sensor)
    {
        Debug.Log("=== CollectObservations Start ===");
        Debug.Log($"Number of agent colliders: {agentColliders.Count}");

        int totalObservations = 0;

        if (agentColliders.Count == 0)
        {
            Debug.LogWarning("No agent colliders available for observations.");
            return;
        }

        foreach (var agentEntry in agentColliders)
        {
            string agentId = agentEntry.Key;
            Collider agentCollider = agentEntry.Value;

            if (agentCollider == null || agentCollider.gameObject == null)
            {
                Debug.LogError($"Null collider or gameObject for Agent ID: {agentId}");
                continue;
            }

            Vector3 rayStart = GetRayStartPosition(agentCollider);
            Debug.Log($"Processing agent: {agentId}, Ray start position: {rayStart}");

            int rayHits = 0;
            int rayMisses = 0;

            // Reset the ray counter for each agent
            for (int i = 0; i < numberOfRays; i++)
            {
                float angle = AngleStep * i;
                Vector3 direction = agentCollider.transform.TransformDirection(Quaternion.Euler(0, angle - raycastAngle / 2, 0) * Vector3.forward);

                if (Physics.Raycast(rayStart, direction, out RaycastHit hit, rayLength))
                {
                    sensor.AddObservation(hit.distance / rayLength);
                    rayHits++;
                    Debug.Log($"Agent {agentId} - Ray {i}: Hit at distance {hit.distance}");

                }
                else
                {
                    sensor.AddObservation(1.0f);
                    rayMisses++;
                    Debug.Log($"Agent {agentId} - Ray {i}: No hit, added 1.0");
                }
                totalObservations++;

                // Visualize the ray for debugging
                Debug.DrawRay(rayStart, direction * rayLength, hit.collider != null ? hitColor : missColor, 0.1f);
            }

            Debug.Log($"Agent {agentId} - Rays: {numberOfRays}, Hits: {rayHits}, Misses: {rayMisses}");

            // Add agent's position and rotation as observations
            sensor.AddObservation(agentCollider.transform.position);
            totalObservations += 3;
            Debug.Log($"Agent {agentId} - Added position: {agentCollider.transform.position}");

            sensor.AddObservation(agentCollider.transform.rotation.eulerAngles.y);
            totalObservations++;
            Debug.Log($"Agent {agentId} - Added rotation Y: {agentCollider.transform.rotation.eulerAngles.y}");
        }

        Debug.Log($"Total observations added: {totalObservations}");
        Debug.Log($"Sensor observation size: {sensor.ObservationSize()}");
        Debug.Log("=== CollectObservations End ===");
    }

    // Modify other methods to use trafficManager as needed
    // For example, in OnEpisodeBegin:
    public override void OnEpisodeBegin()
    {
        Debug.Log("=== OnEpisodeBegin ===");

        // Clear existing agents and colliders
        foreach (var agent in agentInstances.Values)
        {
            if (agent != null)
            {
                Destroy(agent);
            }
        }
        agentInstances.Clear();
        agentColliders.Clear();

        Debug.Log($"Cleared agents. agentInstances count: {agentInstances.Count}, agentColliders count: {agentColliders.Count}");

        // Reset the traffic simulation
        if (trafficSimulationPtr != IntPtr.Zero)
        {
            Traffic_destroy(trafficSimulationPtr);
            trafficSimulationPtr = IntPtr.Zero;
        }

        // Recreate the traffic simulation with a new seed or reset it as required
        trafficSimulationPtr = Traffic_create(2, 12345); // Adjust the number of agents and seed if needed
        Debug.Log($"Created new traffic simulation. Pointer: {trafficSimulationPtr}");

        // Update the agent positions and recreate agent instances
        UpdateAgentPositions();

        Debug.Log($"After UpdateAgentPositions. agentInstances count: {agentInstances.Count}, agentColliders count: {agentColliders.Count}");

        Debug.Log("=== OnEpisodeBegin End ===");
    }

    // Execute the actions decided by the ML model
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        Debug.Log("+++ OnActionReceived +++");
        hasNewActions = true;

        // Process Discrete (High-Level) Actions
        int highLevelActionCount = actionBuffers.DiscreteActions.Length;
        int[] highLevelActions = new int[highLevelActionCount];

        for (int i = 0; i < highLevelActionCount; i++)
        {
            highLevelActions[i] = actionBuffers.DiscreteActions[i];
            Debug.Log($"HighLevelAction[{i}]: {highLevelActions[i]}");
        }

        // Process Continuous (Low-Level) Actions
        int lowLevelActionCount = actionBuffers.ContinuousActions.Length;
        float[] lowLevelActions = new float[lowLevelActionCount];

        for (int i = 0; i < lowLevelActionCount; i++)
        {
            lowLevelActions[i] = actionBuffers.ContinuousActions[i];
            Debug.Log($"LowLevelAction[{i}]: {lowLevelActions[i]}");
        }

        // Step the simulation with the received actions
        Traffic_step(trafficSimulationPtr, highLevelActions, lowLevelActions);

        // Update agent positions based on the simulation step
        UpdateAgentPositions();

        // Calculate reward and determine if the episode should end
        float reward = CalculateReward();  // Implement your actual reward calculation logic
        bool done = CheckIfEpisodeIsDone();  // Implement your actual done condition logic

        SetReward(reward);
        if (done)
        {
            EndEpisode();
        }
    }

    private float CalculateReward()
    {
        // Implement your actual reward logic here
        return 0f;  // Placeholder
    }

    private bool CheckIfEpisodeIsDone()
    {
        // Implement your actual done condition logic here
        return false;  // Placeholder
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        // Continuous actions
        continuousActions[0] = Input.GetAxis("Horizontal"); // Steering
        continuousActions[1] = Input.GetAxis("Vertical"); // Acceleration/Braking

        // Discrete actions
        for (int i = 0; i < discreteActions.Length; i++)
        {
            discreteActions[i] = 0; // Default to 0
        }

        // Example: Set a discrete action based on a condition
        if (Input.GetKey(KeyCode.Space))
        {
            discreteActions[0] = 1;
        }

        Debug.Log("Heuristic method called. Continuous Actions: " +
                  string.Join(", ", continuousActions) +
                  " Discrete Actions: " + string.Join(", ", discreteActions));
    }

    // Update the positions of agents based on the simulation results
    private void UpdateAgentPositions()
    {
        Debug.Log("*** UpdateAgentPositions ***");

        // Get the initial state of agent
        agentPositionsMap = Traffic_get_agent_positions(trafficSimulationPtr);
        agentVelocitiesMap = Traffic_get_agent_velocities(trafficSimulationPtr);
        agentOrientationsMap = Traffic_get_agent_orientations(trafficSimulationPtr);
        agentPreviousPositionsMap = Traffic_get_previous_positions(trafficSimulationPtr);

        // Process the agent positions
        for (int i = 0; i < StringFloatVectorMap_size(agentPositionsMap); i++)
        {
            IntPtr agentIdPtr = StringFloatVectorMap_get_key(agentPositionsMap, i);
            string agentId = Marshal.PtrToStringAnsi(agentIdPtr);

            IntPtr positionPtr = StringFloatVectorMap_get_value(agentPositionsMap, agentIdPtr);
            IntPtr orientionPtr = StringFloatVectorMap_get_value(agentOrientationsMap, agentIdPtr);

            if (positionPtr != IntPtr.Zero && orientionPtr != IntPtr.Zero)
            {
                int positionVectorSize = FloatVector_size(positionPtr);
                int orientionVectorSize = FloatVector_size(orientionPtr);

                if (positionVectorSize > 0 && orientionVectorSize > 0)
                {
                    float x = FloatVector_get(positionPtr, 0);
                    float y = FloatVector_get(positionPtr, 1);
                    float z = FloatVector_get(positionPtr, 2);
                    Debug.Log($"Agent ID: {agentId}, Position: ({x}, {y}, {z})");

                    float roll = FloatVector_get(orientionPtr, 0);
                    float pitch = FloatVector_get(orientionPtr, 1);
                    float yaw = FloatVector_get(orientionPtr, 2);
                    Debug.Log($"Agent ID: {agentId}, Orientation: ({roll}, {pitch}, {yaw})");

                    Vector3 position = new Vector3(x, y, z);

                    // Convert Euler angles to Quaternion, Euler angles (roll, pitch, yaw)
                    Quaternion rotation = Quaternion.Euler(roll * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, pitch * Mathf.Rad2Deg);

                    // Create a new agent instance if it doesn't exist
                    if (!agentInstances.ContainsKey(agentId))
                    {
                        Debug.Log($"Instantiating agentPrefab for Agent ID: {agentId}");

                        // Instantiate the new GameObject with the specified position and orientation
                        GameObject newAgent = Instantiate(agentPrefab, position, rotation);

                        newAgent.transform.SetParent(this.transform);  // Use 'this.transform' to refer to the TrafficSimulationManager's transform
                        agentInstances.Add(agentId, newAgent);

                        // Get the collider component from the instantiated agent
                        Collider agentCollider = newAgent.GetComponent<Collider>();
                        if (agentCollider != null)
                        {
                            Debug.Log($"Agent ID: {agentId}");

                            if (!agentColliders.ContainsKey(agentId))  // Add this check
                            {
                                agentColliders.Add(agentId, agentCollider);
                                Debug.Log($"Added collider for Agent ID: {agentId}");
                            }
                            else
                            {
                                Debug.LogWarning($"Collider for Agent ID: {agentId} already exists. Skipping addition.");
                            }
                        }
                        else
                        {
                            Debug.LogError($"No Collider found on the instantiated agent {agentId}. Please add a Collider to the agent prefab.");
                        }
                    }

                    else
                    {
                        Debug.Log($"Agent ID: {agentId} already exists. Updating position and rotation.");
                        GameObject existingAgent = agentInstances[agentId];
                        existingAgent.transform.position = position;
                        existingAgent.transform.rotation = rotation;
                    }
                }
                else
                {
                    Debug.Log($"Agent ID: {agentId}, Position vector is empty.");
                }

            }
            else
            {
                Debug.Log($"Agent ID: {agentId} already exists. Skipping instantiation.");
            }
        }

        // Clean up the resources
        /*
        StringFloatVectorMap_destroy(agentPositionsMap);
        StringFloatVectorMap_destroy(agentVelocitiesMap);
        StringFloatVectorMap_destroy(agentVelocitiesMap);
        StringFloatVectorMap_destroy(agentOrientationsMap);
        StringFloatVectorMap_destroy(agentPreviousPositionsMap);
        */
    }

    // Helper method to get the ray start position (center of the bounding box)
    private Vector3 GetRayStartPosition(Collider collider)
    {
        return collider.bounds.center + collider.transform.up * (collider.bounds.size.y / 2);
    }

    // Clean up the simulation on destroy
    void OnDestroy()
    {
        Debug.Log("-- OnDestroy --");

        if (trafficSimulationPtr != IntPtr.Zero)
        {
            // Since there is no Traffic_destroy function, directly delete the trafficSimulationPtr
            Marshal.FreeHGlobal(trafficSimulationPtr);
            //Traffic_destroy(trafficSimulationPtr);
            trafficSimulationPtr = IntPtr.Zero;
        }

        // Clean up any other resources, such as agent instances
        foreach (var agentInstance in agentInstances.Values)
        {
            Destroy(agentInstance);
        }
        agentInstances.Clear();

        // Clean up the map references
        if (agentPositionsMap != IntPtr.Zero)
        {
            StringFloatVectorMap_destroy(agentPositionsMap);
            agentPositionsMap = IntPtr.Zero;
        }

        if (agentVelocitiesMap != IntPtr.Zero)
        {
            StringFloatVectorMap_destroy(agentVelocitiesMap);
            agentVelocitiesMap = IntPtr.Zero;
        }

        if (agentOrientationsMap != IntPtr.Zero)
        {
            StringFloatVectorMap_destroy(agentOrientationsMap);
            agentOrientationsMap = IntPtr.Zero;
        }

        if (agentPreviousPositionsMap != IntPtr.Zero)
        {
            StringFloatVectorMap_destroy(agentPreviousPositionsMap);
            agentPreviousPositionsMap = IntPtr.Zero;
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;
using Random = UnityEngine.Random;


// Responsible for ML-Agents specific behaviors (collecting observations, receiving actions, etc.)
public class TrafficAgent : Agent
{
    // Random seed fixing
    [SerializeField] private int seed = 42;

    // Traffic Manager
    [HideInInspector] private TrafficManager trafficManager;

    // Agent Actions
    [HideInInspector] public int highLevelActions;
    [HideInInspector] public float[] lowLevelActions;

    // Color settings for ray visualization
    [HideInInspector] private Color rayHitColor = Color.red;
    [HideInInspector] private Color rayMissColor = Color.green; // Default white
    [SerializeField] private bool debugVisualization = false;

    // Channel Identifier
    [HideInInspector] private Guid channelId = new Guid("621f0a70-4f87-11ea-a6bf-784f4387d1f7");

    // Penalty Settings
    [SerializeField] public float offRoadPenalty = -0.5f;
    [SerializeField] private float onRoadReward = 0.01f;
    [SerializeField] public float collisionWithOtherAgentPenalty = -1.0f;
    [SerializeField] public float medianCrossingPenalty = -1.0f;
    [SerializeField] private float penaltyInterval = 0.5f; // Interval in seconds between penalties
    [HideInInspector] private float lastPenaltyTime = 0f; // Time when the last penalty was applied

    // Collider
    [HideInInspector] private Collider agentCollider;

    //[HideInInspector] private Vector3 previousPosition;

    // Road Layer (for road surfaces)
    int roadLayer;

    // Road Boundary Layer (for boundaries/edges)
    int roadBoundaryLayer;

    // Median boundary Layer
    int medianBoundaryLayer;

    /// <summary>
    /// Initializes the TrafficAgent when the script instance is being loaded.
    ///
    /// This method is called before any Start() method and is used for:
    /// 1. Finding and validating the TrafficManager in the scene.
    /// 2. Initializing the lowLevelActions array for steering, acceleration, and braking.
    ///
    /// Throws an exception if TrafficManager is not found in the scene.
    ///
    /// </summary>
    protected override void Awake()
    {
        LogDebug("TrafficAgent::Awake Start started.");

        Random.InitState(seed);
        base.Awake(); // Optionally call base class method

        // Try to find the TrafficManager in the scene
        if (trafficManager == null)
        {
            trafficManager = FindFirstObjectByType<TrafficManager>();

            if (trafficManager == null)
            {
                Debug.LogError("TrafficManager not found in the scene. Please add a TrafficManager to the scene.");
                throw new Exception("TrafficManager not found in the scene.");
            }
            else
            {
                LogDebug("TrafficManager found successfully.");
            }
        }

        // Initialize lowLevelActions array with appropriate size (e.g., 3 for steering, acceleration, and braking)
        lowLevelActions = new float[3];

        // Road Layer (for road surfaces)
        roadLayer = LayerMask.NameToLayer("Road");

        // Road Boundary Layer (for boundaries/edges)
        roadBoundaryLayer = LayerMask.NameToLayer("RoadBoundary");

        // Median boundary Layer
        medianBoundaryLayer = LayerMask.NameToLayer("MedianBoundary");

        LogDebug("TrafficAgent::Awake completed successfully.");
    }

    /*
    private void Start()
    {
        // Initialize previousPosition with the agent's initial position
        //previousPosition = transform.position;

    }
    */

    /// <summary>
    /// Initializes the TrafficAgent within the ML-Agents framework.
    ///
    /// This method is called once when the agent is first created and is responsible for:
    /// 1. Setting up agent-specific variables and environment.
    /// 2. Resetting agent actions to their initial state.
    /// 3. Setting the maximum number of steps for the agent's episodes.
    ///
    /// It's a crucial part of the ML-Agents setup process, preparing the agent for training or inference.
    ///
    /// Overrides the base Initialize method from the ML-Agents framework.
    /// </summary>
    public override void Initialize()
    {
        LogDebug("TrafficAgent::Initialize started");

        // Get the initial agent positions and create agent instances
        LogDebug($"TrafficAgent Initialize called on {gameObject.name}");

        // Initialize your agent-specific variables here
        base.Initialize();

        MaxStep = trafficManager.MaxSteps; // Max number of steps

        LogDebug("TrafficAgent::Initialize completed successfully.");
    }

    /// <summary>
    /// Prepares the TrafficAgent for a new episode in the ML-Agents training process.
    ///
    /// This method is called at the start of each episode and is responsible for:
    /// 1. Resetting the agent's actions to their initial state.
    /// 2. Resetting the agent's position and rotation.
    /// 3. Logging the current state of the agent and the traffic manager.
    ///
    /// It ensures that each episode starts with a consistent and potentially randomized state,
    /// which is crucial for diverse and effective training experiences in reinforcement learning.
    ///
    /// Overrides the base OnEpisodeBegin method from the ML-Agents framework.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        LogDebug("TrafficAgent::OnEpisodeBegin started");

        base.OnEpisodeBegin();
        ResetAgentActions();
        ResetAgentPositionAndRotation();

        // Clear any accumulated rewards or state
        SetReward(0f);

        LogDebug($"Created agents. agentInstances count: {trafficManager.agentInstances.Count}, agentColliders count: {trafficManager.agentColliders.Count}");
        LogDebug($"Reset agent. Position: {transform.position}, Rotation: {transform.rotation.eulerAngles}");
        LogDebug("TrafficAgent::OnEpisodeBegin completed successfully.");
    }

    /// <summary>
    /// Resets the agent's low-level actions to random values within predefined ranges.
    ///
    /// This method initializes three action values:
    /// 1. Steering angle (in radians): Range [-0.785398, 0.785398] (approximately ±55 degrees)
    /// 2. Acceleration: Range [0.0, 5.0]
    /// 3. Braking: Range [-5.0, 0.0]
    ///
    /// These randomized values help in creating diverse initial conditions for each episode,
    /// which is crucial for effective reinforcement learning training.
    ///
    /// Note: The ranges are hardcoded and may need adjustment based on the specific requirements
    /// of the traffic simulation or learning task.
    /// </summary>
    private void ResetAgentActions()
    {
        LogDebug("TraffocAgemts::ResetAgentActions started.");
        GetRandomActions();
    }

    /// <summary>
    /// Resets the agent's position and rotation to a random location within the defined spawn area.
    ///
    /// This method is responsible for:
    /// 1. Generating a random position and rotation for the agent.
    /// 2. Updating the agent's transform in the Unity scene.
    /// 3. Updating the agent's collider position and rotation.
    /// 4. Synchronizing the agent's position with the C++ traffic simulation.
    ///
    /// Synchronization:
    /// - Updates Unity GameObject transform.
    /// - Updates Unity Collider transform.
    /// - Updates C++ simulation vehicle position.
    ///
    /// Error Handling:
    /// - Checks for null TrafficManager.
    /// - Logs warnings if agent instance or collider is not found.
    ///
    /// Dependencies:
    /// - Requires a properly initialized TrafficManager.
    /// - Relies on TrafficManager's native plugin methods for C++ simulation updates.
    ///
    /// Usage:
    /// Call this method to reset the agent's position, typically at the start of a new episode
    /// or when the agent needs to be repositioned.
    ///
    /// Note:
    /// - Ensure trafficManager is properly initialized before calling this method.
    /// - The method assumes the existence of a C++ backend (TrafficManager.Traffic_get_agent_by_name).
    /// - Consider adding checks or handling for potential errors in C++ method calls.
    ///
    /// </summary>
    public void ResetAgentPositionAndRotation()
    {
        LogDebug("TrafficAgent::ResetAgentPositionAndRotation started.");

        if (trafficManager != null)
        {
            // Sample and initialize agents state, e.g. position, speed, orientation
            TrafficManager.Traffic_sampleAndInitializeAgents(trafficManager.trafficSimulationPtr);

            // Obtain pointer to traffic vehicle state
            IntPtr vehiclePtr = TrafficManager.Traffic_get_agent_by_name(trafficManager.trafficSimulationPtr, gameObject.name);

            // Update the vehicle's position in the C++ simulation
            Vector3 position = new Vector3(
                TrafficManager.Vehicle_getX(vehiclePtr),
                TrafficManager.Vehicle_getY(vehiclePtr),
                TrafficManager.Vehicle_getZ(vehiclePtr)
            );

            // Update the vehicle's rotation in the C++ simulation
            float yaw = Mathf.Rad2Deg * TrafficManager.Vehicle_getYaw(vehiclePtr);
            Quaternion rotation = Quaternion.Euler(0, yaw, 0);

            LogDebug($"Resetting agent {gameObject.name} - Position: {position}, Rotation: {rotation}");

            try
            {
                // Update the agent's position and rotation in the Unity scene
                UpdateAgentTransform(position, rotation);
                UpdateAgentCollider(position, rotation);

                // Update the agent's position in the C++ traffic simulation
                UpdateTrafficSimulation(position, rotation);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error updating C++ simulation for agent {gameObject.name}: {ex.Message}");
            }
        }
        else
        {
            Debug.LogError($"TrafficManager is null for agent {gameObject.name}");
        }

        LogDebug("TrafficAgent::ResetAgentPositionAndRotation completed successfully.");
    }

    /// <summary>
    /// Updates the transform (position and rotation) of the agent's instance in the Unity scene.
    ///
    /// This method attempts to:
    /// 1. Retrieve the TrafficAgent instance associated with this agent's name.
    /// 2. Set the position and rotation of the found instance to the provided values.
    ///
    /// Parameters:
    /// - position: The new Vector3 position to set for the agent.
    /// - rotation: The new Quaternion rotation to set for the agent.
    ///
    /// Error Handling:
    /// - Logs a warning if the agent instance is not found in trafficManager.agentInstances.
    ///
    /// Usage:
    /// Call this method when you need to update the agent's position and rotation in the Unity scene,
    /// such as after calculating a new position or when resetting the agent.
    ///
    /// Dependencies:
    /// - Requires a properly initialized TrafficManager with populated agentInstances dictionary.
    /// - Assumes the current GameObject's name is used as the key in agentInstances.
    ///
    /// Note:
    /// - This method only updates the Unity Transform component and does not affect any underlying
    ///   simulation or physics state.
    /// - Consider adding null checks for trafficManager if there's a possibility it might not be initialized.
    /// - For comprehensive state updates, ensure this is called in conjunction with updates to
    ///   colliders, rigid bodies, and any backend simulation state.
    ///
    /// </summary>
    /// <param name="position">The new position to set for the agent.</param>
    /// <param name="rotation">The new rotation to set for the agent.</param>
    private void UpdateAgentTransform(Vector3 position, Quaternion rotation)
    {
        if (trafficManager.agentInstances.TryGetValue(gameObject.name, out TrafficAgent agentInstance))
        {
            agentInstance.transform.SetPositionAndRotation(position, rotation);
        }
        else
        {
            Debug.LogWarning($"Instance not found for agent {gameObject.name}");
        }
    }

    /// <summary>
    /// Updates the transform (position and rotation) of the agent's collider in the Unity scene.
    ///
    /// This method attempts to:
    /// 1. Retrieve the Collider component associated with this agent's name from trafficManager.
    /// 2. Set the position and rotation of the found collider's transform to the provided values.
    ///
    /// Parameters:
    /// - position: The new Vector3 position to set for the agent's collider.
    /// - rotation: The new Quaternion rotation to set for the agent's collider.
    ///
    /// Error Handling:
    /// - Logs a warning if the agent's collider is not found in trafficManager.agentColliders.
    ///
    /// Usage:
    /// Call this method when you need to update the agent's collider position and rotation,
    /// typically in conjunction with updating the agent's main transform or during reset operations.
    ///
    /// Dependencies:
    /// - Requires a properly initialized TrafficManager with populated agentColliders dictionary.
    /// - Assumes the current GameObject's name is used as the key in agentColliders.
    ///
    /// Note:
    /// - This method only updates the Unity Collider's transform and does not affect the agent's
    ///   main transform or any underlying simulation state.
    /// - Consider adding null checks for trafficManager if there's a possibility it might not be initialized.
    /// - Ensure this method is called in coordination with other state update methods (e.g., UpdateAgentTransform)
    ///   to maintain consistency across all representations of the agent.
    /// - Updating the collider's transform directly might have implications for physics simulations;
    ///   ensure this aligns with your intended physics behavior.
    ///
    /// </summary>
    /// <param name="position">The new position to set for the agent's collider.</param>
    /// <param name="rotation">The new rotation to set for the agent's collider.</param>
    private void UpdateAgentCollider(Vector3 position, Quaternion rotation)
    {
        if (trafficManager.agentColliders.TryGetValue(gameObject.name, out Collider agentCollider))
        {
            agentCollider.transform.SetPositionAndRotation(position, rotation);
        }
        else
        {
            Debug.LogWarning($"Collider not found for agent {gameObject.name}");
        }
    }

    /// <summary>
    /// Updates the position of the agent in the C++ traffic simulation backend.
    ///
    /// This method performs the following operations:
    /// 1. Retrieves a pointer to the agent's vehicle in the C++ simulation using its name.
    /// 2. Sets the X, Y, and Z coordinates of the vehicle in the C++ simulation to match the provided position.
    ///
    /// Parameters:
    /// - position: A Vector3 representing the new position to set in the C++ simulation.
    ///
    /// Dependencies:
    /// - Requires a properly initialized TrafficManager with a valid trafficSimulationPtr.
    /// - Relies on external C++ methods (likely from a native plugin) for interacting with the simulation:
    ///   - TrafficManager.Traffic_get_agent_by_name
    ///   - TrafficManager.Vehicle_setX/Y/Z
    ///
    /// Usage:
    /// Call this method when you need to synchronize the agent's position in the Unity scene
    /// with its representation in the C++ traffic simulation backend.
    ///
    /// Error Handling:
    /// - This method doesn't include explicit error handling. Consider adding checks for null pointers
    ///   or invalid states, especially if the C++ methods can fail or return error codes.
    ///
    /// Performance Considerations:
    /// - This method makes multiple calls to external C++ functions, which may have performance implications
    ///   if called frequently. Consider batching updates if possible.
    ///
    /// Note:
    /// - Ensure that the C++ simulation is properly initialized before calling this method.
    /// - The method assumes that the agent's name in Unity matches its identifier in the C++ simulation.
    /// - Only position is updated; if rotation or other properties need synchronization, additional methods may be needed.
    /// - Consider adding logging or debug options to track synchronization between Unity and C++ simulation.
    ///
    /// </summary>
    /// <param name="position">The new position to set for the agent in the C++ simulation.</param>
    private void UpdateTrafficSimulation(Vector3 position,  Quaternion rotation)
    {
        IntPtr vehiclePtr = TrafficManager.Traffic_get_agent_by_name(trafficManager.trafficSimulationPtr, gameObject.name);

        // Update the vehicle's position in the C++ simulation
        TrafficManager.Vehicle_setX(vehiclePtr, position.x);
        TrafficManager.Vehicle_setY(vehiclePtr, position.y);
        TrafficManager.Vehicle_setZ(vehiclePtr, position.z);

        // Update the vehicle's rotation in the C++ simulation
        // Note: we arbitrarily set the yaw and steering angle to be the same for initialization purpose
        TrafficManager.Vehicle_setSteering(vehiclePtr, rotation.y);
        TrafficManager.Vehicle_setYaw(vehiclePtr, rotation.y);
    }

    /// <summary>
    /// Collects and provides observations about the agent's environment to the ML-Agents neural network.
    ///
    /// This method is crucial for the agent's decision-making process, gathering information such as:
    /// 1. Raycast data for detecting nearby agents and road boundaries.
    /// 2. Agent's position and rotation.
    /// 3. Agent's speed and orientation.
    ///
    /// Key Components:
    /// - Raycast Observations:
    ///   - Number of rays: Defined by trafficManager.numberOfRays
    ///   - For each ray: Distance (normalized) and type of object hit (agent, boundary, or other)
    /// - Position: Agent's current position in 3D space
    /// - Rotation: Agent's yaw (y-axis rotation)
    /// - Speed: Normalized velocity magnitude (assumes max speed of 50)
    /// - Orientation: Normalized y-rotation to range [-1, 1]
    ///
    /// Error Handling:
    /// - Checks for null TrafficManager and missing agent colliders
    /// - Logs warnings for missing components (e.g., Rigidbody)
    ///
    /// Performance Considerations:
    /// - Raycasting is computationally expensive; optimize the number of rays if needed
    /// - Extensive debug logging is present; consider conditional logging for production
    ///
    /// Note:
    /// - Ensure consistency between observations collected here and the neural network's input expectations
    /// - Some observations are commented out (full rotation, roll, pitch); uncomment if needed
    /// - The method assumes specific tags ("TrafficAgent", "RoadBoundary") for object identification
    ///
    /// </summary>
    /// <param name="sensor">The VectorSensor used to collect and send observations to the neural network</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        LogDebug("TrafficAgent::CollectObservations started.");

        LogDebug($"CollectObservations called. trafficManager null? {trafficManager == null}");
        LogDebug($"trafficManager.agentColliders null? {trafficManager.agentColliders == null}");
        LogDebug($"trafficManager.agentColliders count: {trafficManager.agentColliders?.Count ?? 0}");
        LogDebug($"Number of agent colliders: {trafficManager.agentColliders.Count}");
        LogDebug($"Collected {sensor.ObservationSize()} observations");

        if (!InitializeTrafficManager(sensor))
            return;

        // The RayPerceptionSensorComponent3D will automatically add its observations
        CollectPositionAndRotationObservations(sensor);
        CollectSpeedObservations(sensor);

        LogDebug("TrafficAgent::CollectObservations completed successfully.");
    }

    /// <summary>
    /// Initializes and validates the TrafficManager and agent collider for the current agent.
    ///
    /// This method performs the following checks:
    /// 1. Verifies that the TrafficManager is not null and contains a collider for this agent.
    /// 2. Retrieves and stores the agent's collider from the TrafficManager.
    /// 3. Confirms that the retrieved collider is not null.
    ///
    /// Parameters:
    /// - sensor: A VectorSensor parameter, currently unused in the method body.
    ///   Consider removing if not needed or document its intended future use.
    ///
    /// Returns:
    /// - true if initialization is successful (TrafficManager and collider are valid).
    /// - false if any initialization step fails.
    ///
    /// Error Handling:
    /// - Logs a warning if TrafficManager is null or doesn't contain the agent's collider.
    /// - Logs an error if the agent's collider is found in the dictionary but is null.
    ///
    /// Usage:
    /// Call this method before performing operations that depend on TrafficManager or the agent's collider.
    /// Typically used in initialization methods or before collecting observations.
    ///
    /// Dependencies:
    /// - Requires a properly set up TrafficManager with populated agentColliders dictionary.
    /// - Assumes the current GameObject's name is used as the key in agentColliders.
    ///
    /// Side Effects:
    /// - Sets the agentCollider field if initialization is successful.
    ///
    /// Note:
    /// - The method name suggests TrafficManager initialization, but it primarily validates existing setup.
    /// - Consider renaming to "ValidateTrafficManagerSetup" for clarity if no actual initialization is performed.
    /// - The VectorSensor parameter is currently unused; consider removing or implementing its use.
    /// - Ensure this method is called at appropriate times to guarantee proper agent setup.
    ///
    /// </summary>
    /// <param name="sensor">A VectorSensor, currently unused in the method.</param>
    /// <returns>Boolean indicating successful initialization/validation of TrafficManager and agent collider.</returns>
    private bool InitializeTrafficManager(VectorSensor sensor)
    {
        if (trafficManager == null || !trafficManager.agentColliders.ContainsKey(gameObject.name))
        {
            Debug.LogWarning($"TrafficManager or agent collider not properly initialized for {gameObject.name}");
            return false;
        }

        agentCollider = trafficManager.agentColliders[gameObject.name];

        if (agentCollider == null)
        {
            Debug.LogError($"Agent collider not found for {gameObject.name}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Collects and adds the agent's position and rotation observations to the sensor.
    ///
    /// This method performs the following operations:
    /// 1. Adds the agent's current position (Vector3) as an observation.
    /// 2. Adds the agent's rotation around the Y-axis (yaw) as an observation.
    ///
    /// Observations added:
    /// - Position: Vector3 (x, y, z coordinates)
    /// - Rotation: Float (y-axis rotation in degrees)
    ///
    /// Note on rotation:
    /// - Only the Y-axis rotation (yaw) is currently observed.
    /// - Full rotation (Quaternion) and Euler angles (Vector3) observations are commented out.
    ///
    /// Usage:
    /// Call this method as part of the agent's observation collection process, typically within
    /// a larger CollectObservations method.
    ///
    /// Customization Options:
    /// - Uncomment transform.rotation to observe full rotation as a Quaternion (4 float values).
    /// - Uncomment transform.rotation.eulerAngles to observe rotation as Euler angles (3 float values).
    ///
    /// Performance Considerations:
    /// - Adding observations increases the size of the agent's observation space.
    /// - Balance the amount of information with the complexity of the learning task.
    ///
    /// Note:
    /// - Ensure the neural network model is configured to handle the number of observations provided.
    /// - Consider normalizing position values if the agent operates in a large world space.
    /// - The choice of rotation representation (Euler angle vs Quaternion) can affect learning;
    ///   choose based on your specific requirements and the nature of the task.
    /// - If only Y-rotation is relevant to your task, the current setup is appropriate.
    ///   Otherwise, consider including full rotation information.
    ///
    /// </summary>
    /// <param name="sensor">The VectorSensor to which observations are added.</param>
    private void CollectPositionAndRotationObservations(VectorSensor sensor)
    {
        // Add agent's position and rotation as observations
        sensor.AddObservation(transform.position);    // Adds (x, y, z) in world space
        //sensor.AddObservation(transform.localPosition); // Adds (x, y, z) relative to parent

        // Add rotation observations (we might want to use Quaternion or Euler angles)
        sensor.AddObservation(transform.rotation.eulerAngles.y); // Adds only the y-rotation, often represents the "yaw" or horizontal rotation.
        // Orientation (only y rotation, normalized to [-1, 1])
        //sensor.AddObservation(transform.rotation.eulerAngles.y / 180.0f - 1.0f);
        //sensor.AddObservation(transform.rotation.eulerAngles); // Adds (x, y, z) Euler angles
        //sensor.AddObservation(transform.rotation);     // Adds (x, y, z, w) quaternion in world space
        //sensor.AddObservation(transform.localRotation); // Adds (x, y, z, w) quaternion relative to parent

        // We might want to observe the agent's forward direction
        //sensor.AddObservation(transform.forward); // Adds (x, y, z) direction vector. This adds the forward direction of the object as a normalized vector in world space. It's particularly useful for understanding the direction the agent is facing, regardless of its position.
    }

    /// <summary>
    /// Collects and adds the agent's position and rotation observations to the sensor.
    ///
    /// This method performs the following operations:
    /// 1. Adds the agent's current position (Vector3) as an observation.
    /// 2. Adds the agent's rotation around the Y-axis (yaw) as an observation.
    ///
    /// Observations added:
    /// - Position: Vector3 (x, y, z coordinates)
    /// - Rotation: Float (y-axis rotation in degrees)
    ///
    /// Note on rotation:
    /// - Only the Y-axis rotation (yaw) is currently observed.
    /// - Full rotation (Quaternion) and Euler angles (Vector3) observations are commented out.
    ///
    /// Usage:
    /// Call this method as part of the agent's observation collection process, typically within
    /// a larger CollectObservations method.
    ///
    /// Customization Options:
    /// - Uncomment transform.rotation to observe full rotation as a Quaternion (4 float values).
    /// - Uncomment transform.rotation.eulerAngles to observe rotation as Euler angles (3 float values).
    ///
    /// Performance Considerations:
    /// - Adding observations increases the size of the agent's observation space.
    /// - Balance the amount of information with the complexity of the learning task.
    ///
    /// Note:
    /// - Ensure the neural network model is configured to handle the number of observations provided.
    /// - Consider normalizing position values if the agent operates in a large world space.
    /// - The choice of rotation representation (Euler angle vs Quaternion) can affect learning;
    ///   choose based on your specific requirements and the nature of the task.
    /// - If only Y-rotation is relevant to your task, the current setup is appropriate.
    ///   Otherwise, consider including full rotation information.
    ///
    /// </summary>
    /// <param name="sensor">The VectorSensor to which observations are added.</param>
    private void CollectSpeedObservations(VectorSensor sensor)
    {
        // Debug logs to check method calls
        LogDebug("TrafficAgent::CollectSpeedObservations started.");

        // Agent's own speed and orientation
        Rigidbody rb = GetComponent<Rigidbody>();
        rb.isKinematic = false; // true;
        rb.useGravity = false;
        float speed = 0.0f;

        // Calculate velocity manually
        /*
        Vector3 currentPosition = transform.position;
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f); // Prevent divide by zero
        Vector3 velocity = (currentPosition - previousPosition) / deltaTime;
        */

        if (rb != null)
        {
            speed = rb.linearVelocity.magnitude; // Normalize speed if needed (max speed = 50)

            // Check for invalid speed values (NaN or Infinity)
            if (!float.IsNaN(speed) && !float.IsInfinity(speed))
            {
                sensor.AddObservation(speed);
            }
        }
        else
        {
            sensor.AddObservation(speed); // No Rigidbody attached or zero velocity
            Debug.LogWarning($"No Rigidbody found on {gameObject.name}. Using 0 for speed observation.");
        }

        // Update previousPosition for the next frame
        //previousPosition = currentPosition;

        LogDebug($"Observations: Position = {transform.position}, Velocity = {speed}");
    }

    /// <summary>
    /// Generates heuristic actions for the agent when the Behavior Type is set to "Heuristic Only".
    ///
    /// This method serves as a fallback or testing mechanism, providing manually defined or random actions
    /// instead of using the trained neural network. It's responsible for:
    /// 1. Generating a random discrete action (high-level decision).
    /// 2. Generating random continuous actions for steering, acceleration, and braking.
    ///
    /// Action Breakdown:
    /// - Discrete Action:
    ///   - Index 0: Random integer between 0 and 4 (inclusive)
    /// - Continuous Actions:
    ///   - Index 0: Steering angle in radians (range: -45 to 45 degrees, converted to radians)
    ///   - Index 1: Acceleration (range: 0.0 to 5.0)
    ///   - Index 2: Braking (range: -5.0 to 0.0)
    ///
    /// Usage:
    /// - Automatically called by ML-Agents when Behavior Type is "Heuristic Only".
    /// - Useful for debugging, testing agent behavior, or providing a baseline performance.
    ///
    /// Note:
    /// - Ensure the action ranges here match those expected by OnActionReceived and the training configuration.
    /// - The random nature of actions may lead to erratic behavior - this is expected in heuristic mode.
    /// - Consider implementing more sophisticated heuristics for better baseline performance if needed.
    /// - Logging of actions may impact performance; consider using conditional logging for extensive testing.
    ///
    /// </summary>
    /// <param name="actionsOut">Buffer to store the generated actions. Contains both discrete and continuous action arrays.</param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Debug logs to check method calls
        LogDebug("TrafficAgent::Heuristic started.");

        // Get discrete actions array from ActionBuffers
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = UnityEngine.Random.Range(0, 5); // This will give a random integer from 0 to 4 inclusive

        float minAngleRad = -Mathf.PI / 4f; // -45 degrees in radians (-0.785398...)
        float maxAngleRad = Mathf.PI / 4f;  // 45 degrees in radians (0.785398...)

        // For continuous actions, assuming index 0 is steering and 1 is acceleration and 2 is braking:
        var continuousActions = actionsOut.ContinuousActions;

        // Sample a random angle between -45 and 45 degrees and convert to radians for steering
        continuousActions[0] = UnityEngine.Random.Range(minAngleRad, maxAngleRad); // Steering

        // Sample a random value for acceleration
        continuousActions[1] = UnityEngine.Random.Range(0.0f, 4.5f); // Acceleration

        // Sample a random value for braking
        continuousActions[2] = UnityEngine.Random.Range(-4.0f, 0.0f); // Braking

        LogDebug("Heuristic method called. Discrete Actions: " +
                 string.Join(", ", discreteActions) +
                " Continuous Actions: " + string.Join(", ", continuousActions)
        );

        LogDebug("TrafficAgent::Heuristic completed successfully.");
    }

    /// <summary>
    /// Processes and applies actions received from the machine learning model to the agent.
    ///
    /// This method is a key part of the ML-Agents reinforcement learning loop, responsible for:
    /// 1. Receiving action decisions from the trained model.
    /// 2. Processing both discrete (high-level) and continuous (low-level) actions.
    /// 3. Storing these actions for later use in agent behavior.
    ///
    /// Action Breakdown:
    /// - Discrete Actions: Stored in highLevelActions (currently using only the first discrete action).
    /// - Continuous Actions: Stored in lowLevelActions array (e.g., steering, acceleration, braking).
    ///
    /// Key Points:
    /// - Overrides the base OnActionReceived method from ML-Agents.
    /// - Logs received actions for debugging purposes.
    /// - Does not directly apply physical movements (commented out code suggests this was moved to FixedUpdate).
    ///
    /// Usage:
    /// This method is automatically called by the ML-Agents framework when new actions are decided.
    /// The stored actions should be used in physics update methods (like FixedUpdate) to control the agent.
    ///
    /// Note:
    /// - Ensure that the sizes of actionBuffers match the expected action space defined in your training configuration.
    /// - The commented-out movement code indicates a separation of decision-making and physics application, which is a good practice.
    /// - Consider adding validation or clamping of received actions if necessary.
    /// - Logging of actions may impact performance; consider using conditional logging for production.
    ///
    /// </summary>
    /// <param name="actionBuffers">Contains the discrete and continuous actions decided by the ML model.</param>
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        LogDebug("TrafficAgent::OnActionReceived started.");

        LogDebug($"Discrete actions: {string.Join(", ", actionBuffers.DiscreteActions)}");
        LogDebug($"Continuous actions: {string.Join(", ", actionBuffers.ContinuousActions)}");

        // Process Discrete (High-Level) Actions
        highLevelActions = actionBuffers.DiscreteActions[0];

        // Process Continuous (Low-Level) Actions
        int lowLevelActionCount = actionBuffers.ContinuousActions.Length;

        for (int i = 0; i < lowLevelActionCount; i++)
        {
            // Fill the arrays with data from actionBuffers
            lowLevelActions[i] = actionBuffers.ContinuousActions[i];
        }

        // Move this to FixedUpdate
        // rb.MovePosition(transform.position + transform.forward * lowLevelActions[1] * Time.fixedDeltaTime);
        // transform.Rotate(Vector3.up, lowLevelActions[0] * Time.fixedDeltaTime);

        LogDebug("TrafficAgent::OnActionReceived completed successfully.");
    }

    /// <summary>
    /// Executes at a fixed time interval (default 0.02 seconds) for consistent updates, primarily used for debugging visualization.
    ///
    /// This method is responsible for:
    /// 1. Logging debug information at the start and end of each fixed update cycle.
    /// 2. Conditionally calling DrawDebugRays() for visual debugging when enabled.
    ///
    /// Key points:
    /// - Runs at a fixed time step, independent of frame rate.
    /// - Used for visualization purposes only; does not affect agent movement or physics.
    /// - Debug ray visualization is controlled by either local or TrafficManager debug flags.
    ///
    /// Visualization Control:
    /// - Local control: debugVisualization (boolean field in this class)
    /// - Global control: trafficManager.debugVisualization (if TrafficManager is available)
    ///
    /// Note:
    /// - FixedUpdate is ideal for physics-related code, though not used for that purpose here.
    /// - Ensure DrawDebugRays() is optimized, as it runs frequently when debugging is enabled.
    /// - Consider adding more debug visualizations here if needed, but be mindful of performance.
    /// - The commented explanation about Update() vs FixedUpdate() provides useful context for developers.
    ///
    /// </summary>
    private void FixedUpdate()
    {
        /*
         * In Unity, the Update() method, is called once per frame and is primarily used for handling tasks
         * that need to be executed in sync with the frame rate, such as processing user input,
         * updating non-physics game logic, and rendering-related updates.
         */
        LogDebug("TrafficAgent::FixedUpdate started.");

        // Only draw debug rays if visualization is enabled
        if (debugVisualization || (trafficManager != null && trafficManager.debugVisualization))
        {
            DrawDebugRays();
        }

        ActionBuffers storedActions = GetStoredActionBuffers();
        LogDebug($"ActionBuffers: continous: {storedActions.ContinuousActions} discrete: {storedActions.DiscreteActions}");

        LogDebug("TrafficAgent::FixedUpdate completed successfully.");
    }

    /// <summary>
    /// Executes debugging operations every frame in the Unity Editor.
    ///
    /// This method is currently set up for potential debugging use, with its main functionality commented out.
    /// It's designed to run only in the Unity Editor environment, not in built applications.
    ///
    /// Current (Commented) Functionality:
    /// - Logs the rotation (in Euler angles) of the agent every frame.
    ///
    /// Usage:
    /// - Uncomment the Debug.Log line to activate rotation logging.
    /// - Add other debugging operations as needed for development and testing.
    ///
    /// Note:
    /// - This method runs every frame, so be cautious about performance impact when adding debug operations.
    /// - The #if UNITY_EDITOR directive ensures these debug operations only occur in the Unity Editor.
    /// - Consider using this method to visualize or log other important agent states or behaviors during development.
    /// - For extensive debugging, consider creating a separate debug mode toggle to avoid cluttering this method.
    ///
    /// </summary>
    void Update()
    {
        LogDebug("TrafficAgent::Update started.");
        LogDebug($"TrafficAgent::Update: Agent {gameObject.name} rotation: {transform.rotation.eulerAngles}");

        /*
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, 0.1f);
        foreach (var hitCollider in hitColliders)
        {
            Debug.Log($"Overlapping with: {hitCollider.gameObject.name}");
        }
        */

        LogDebug("TrafficAgent::Update completed successfully.");
    }

    /// <summary>
    /// Visualizes raycasts in the Unity scene view for debugging and development purposes.
    ///
    /// This method performs the following operations:
    /// 1. Validates the TrafficManager and agent's collider.
    /// 2. Calculates the starting position for raycasts using the agent's collider.
    /// 3. Casts multiple rays in a fan-like pattern in front of the agent.
    /// 4. Visualizes these rays in the scene view, with different colors for hits and misses.
    ///
    /// Key components:
    /// - trafficManager: Provides configuration for raycasting (angle, number of rays, ray length).
    /// - agentCollider: Used to determine the starting point of raycasts.
    /// - rayHitColor and rayMissColor: Colors used to visualize ray hits and misses respectively.
    ///
    /// Raycast Configuration:
    /// - Number of rays: Defined by trafficManager.numberOfRays
    /// - Total angle covered: trafficManager.raycastAngle
    /// - Ray length: trafficManager.rayLength
    ///
    /// Usage:
    /// Call this method in Update() or a similar frequent update loop when debugging is needed.
    /// Only visible in the Unity Editor's Scene view.
    ///
    /// Note:
    /// - Ensure TrafficManager is properly initialized and contains the agent's collider information.
    /// - This method is computationally expensive and should be used for debugging only.
    /// - Consider adding a debug flag to enable/disable this visualization easily.
    ///
    /// </summary>
    private void DrawDebugRays()
    {
        if (trafficManager == null || !trafficManager.agentColliders.ContainsKey(gameObject.name))
        {
            Debug.LogError("TrafficManager is null in DrawDebugRays");
            return;
        }

        Collider agentCollider = trafficManager.agentColliders[gameObject.name];
        if (agentCollider == null)
        {
            Debug.LogError("Agent Collider is null in DrawDebugRays");
            return;
        }

        Vector3 rayStart = GetRayStartPosition(agentCollider);

        float delta_angle = trafficManager.raySensor.MaxRayDegrees / (trafficManager.raySensor.RaysPerDirection - 1);

        for (int i = 0; i < trafficManager.raySensor.RaysPerDirection; i++)
        {
            float angle = delta_angle * i;
            Vector3 direction = transform.TransformDirection(Quaternion.Euler(0, angle - trafficManager.raySensor.MaxRayDegrees / 2, 0) * Vector3.forward);

            // Ray hit at a certain distance
            if (Physics.Raycast(rayStart, direction, out RaycastHit hit, trafficManager.raySensor.RayLength))
            {
                Debug.DrawRay(rayStart, direction * hit.distance, rayHitColor, 0.0f);
            }
            else // Ray did not hit
            {
                Debug.DrawRay(rayStart, direction * trafficManager.raySensor.RayLength, rayMissColor, 0.0f);
            }
        }
    }

    /// <summary>
    /// Calculates the starting position for raycasting from the top center of a Collider's bounding box.
    ///
    /// This helper method determines an optimal point to start raycasts from the given Collider:
    /// - Uses the center of the Collider's bounding box as a base.
    /// - Offsets the position upwards by half the height of the bounding box.
    ///
    /// Use cases:
    /// - Ideal for initiating raycasts for object detection or environment sensing.
    /// - Useful in scenarios like detecting overhead obstacles or performing top-down environment scans.
    ///
    /// Note:
    /// - The method assumes the Collider's up direction is aligned with the desired "up" for the raycast.
    /// - For non-uniform or rotated Colliders, consider additional adjustments if needed.
    /// - Ensure the Collider parameter is not null before calling this method.
    ///
    /// </summary>
    /// <param name="collider">The Collider used to determine the raycast start position. Must not be null.</param>
    /// <returns>A Vector3 representing the top-center point of the Collider's bounding box.</returns>
    private Vector3 GetRayStartPosition(Collider collider)
    {
        return collider.bounds.center + collider.transform.up * (collider.bounds.size.y / 2);
    }

    /// <summary>
    /// Handles the initial collision between the agent and other objects in the environment.
    ///
    /// This method is called once when the agent first collides with another Collider. It manages:
    /// 1. Collision Detection and Logging:
    ///    - Logs detailed information about the collision for debugging purposes.
    ///    - Specifically checks for collisions with objects tagged or layered as "RoadBoundary".
    /// 2. Collision-based Rewards/Penalties:
    ///    - RoadBoundary: Applies offRoadPenalty for going off the designated path.
    ///    - TrafficAgent: Applies collisionWithOtherAgentPenalty for colliding with other agents.
    ///    - (Commented out) Default case: Can apply a small penalty for unspecified collisions.
    ///
    /// Key components:
    /// - offRoadPenalty: Penalty for leaving the road.
    /// - collisionWithOtherAgentPenalty: Penalty for colliding with other agents.
    /// - AddReward: Method assumed to be part of a reinforcement learning framework (e.g., ML-Agents).
    ///
    /// Usage:
    /// This method is automatically called by Unity's physics system when a collision begins.
    /// No manual invocation is needed.
    ///
    /// Note:
    /// - Ensure proper tagging and layer assignment of objects in the Unity editor.
    /// - The commented section suggests starting simple and focusing on rewarding results rather than actions.
    /// - Penalty values can be adjusted to fine-tune the agent's learning process.
    /// - Consider uncommenting and customizing the default case if needed for comprehensive collision handling.
    /// </summary>
    /// <param name="collision">Contains information about the collision, including references to the colliding objects.</param>
    private void OnCollisionEnter(Collision collision)
    {
        LogDebug("TrafficAgent::OnCollisionEnter started.");

        if (collision.gameObject == null || string.IsNullOrEmpty(collision.gameObject.tag))
        {
            LogDebug("Collision object is null or has no tag.");
            return;
        }

        LogDebug($"Collision detected with {collision.gameObject.name}, tag: {collision.gameObject.tag}, layer: {LayerMask.LayerToName(collision.gameObject.layer)}");

        /*
        Note: Perhaps the best advice is to start simple and only add complexity as needed.
        In general, you should reward results rather than actions you think will lead to the desired results.
        */

        // Handle collision based on object tag
        switch (collision.gameObject.tag)
        {
            case "TrafficAgent": // "Obstacle"
                // Penalize the agent for colliding with another traffic participant
                AddReward(collisionWithOtherAgentPenalty);
                LogDebug($"Collided with another agent! Penalty added: {collisionWithOtherAgentPenalty}");
                break;

            case "RoadBoundary":
                // Penalize the agent for going off the road
                AddReward(offRoadPenalty);
                LogDebug($"Hit road boundary! Penalty added: {offRoadPenalty}");
                break;

            case "MedianBoundary":
                AddReward(medianCrossingPenalty);
                LogDebug($"Crossed the median! Penalty added: {medianCrossingPenalty}");
                break;

                default:
                    // For any other collision, we might want to add a small negative reward
                    AddReward(-0.01f); // Negligible penalty for unspecified collisions
                    LogDebug("Unspecified collision! Small penalty added: -0.1");
                    break;
        }

        LogDebug("TrafficAgent::OnCollisionEnter completed successfully.");
    }

    /// <summary>
    /// Handles continuous collision between the agent and other objects in the environment.
    ///
    /// This method is called every fixed frame-rate frame while a collision is ongoing. It manages:
    /// 1. Road Interaction:
    ///    - Applies a small positive reward (0.01) when the agent is on the "Road" layer.
    ///    - Encourages the agent to stay on the designated driving surface.
    /// 2. Off-Road Penalty:
    ///    - Applies a larger negative reward (-0.1) when the agent is not on the "Road" layer.
    ///    - Discourages the agent from leaving the designated driving area.
    ///
    /// Key components:
    /// - Road Layer: Used to identify the proper driving surface.
    /// - AddReward: Method assumed to be part of a reinforcement learning framework (e.g., ML-Agents).
    ///
    /// Usage:
    /// This method is automatically called by Unity's physics system during ongoing collisions.
    /// No manual invocation is needed.
    ///
    /// Note:
    /// - Ensure that road objects are assigned to the "Road" layer in the Unity editor.
    /// - The reward values (0.01 for on-road, -0.1 for off-road) can be adjusted to fine-tune
    ///   the agent's behavior and learning process.
    /// - Continuous reward/penalty application can significantly impact the agent's learning,
    ///   so careful balancing may be required.
    /// </summary>
    /// <param name="collision">Contains information about the ongoing collision,
    /// including references to the colliding objects.</param>
    private void OnCollisionStay(Collision collision)
    {
        LogDebug("TrafficAgent::OnCollisionStay started.");

        // Check if the agent is colliding with the road boundary or median boundary
        if (collision.gameObject.layer == roadLayer)
        {
            // Reward for staying on the road (road surface)
            AddReward(onRoadReward);
            LogDebug("Agent rewarded for staying on the road.");
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("MedianBoundary"))
        {
            // Penalize for staying on the median boundary
            AddReward(medianCrossingPenalty);
            LogDebug("Agent penalized for staying on the median boundary.");
        }
        else
        {
            // Penalize for being off the road or any other unspecified collision
            AddReward(-0.1f);
            LogDebug("Agent penalized for being off-road or unspecified collision.");
        }

        LogDebug("TrafficAgent::OnCollisionStay completed successfully.");
    }

    /// <summary>
    /// Handles the event when the agent's Collider stops colliding with another Collider.
    ///
    /// This method specifically checks for the end of collisions with objects on the "Road" layer:
    /// - This can be used to detect when the agent has left the designated driving surface.
    ///
    ///
    /// Usage:
    /// This method is automatically called by Unity's physics system when a collision ends.
    /// No manual invocation is needed.
    ///
    /// Note:
    /// - Ensure that road objects are assigned to the "Road" layer in the Unity editor.
    /// - This method uses Unity's built-in layer system for efficient collision detection.
    /// </summary>
    /// <param name="collision">Contains information about the collision that has ended,
    /// including references to the colliding objects.</param>
    public void OnCollisionExit(Collision collision)
    {
        LogDebug("TrafficAgent::OnCollisionExit started.");

        // Check if the agent has left the road
        if (collision.gameObject.layer == roadLayer)
        {
            // Reward for exiting the boundary and returning to the road
            AddReward(0.1f);
            LogDebug("Agent left the road and returned to it. Reward added.");
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("RoadBoundary"))
        {
            // Reward for exiting the road boundary (right/left boundary)
            AddReward(0.1f);
            LogDebug("Agent left the road boundary. Reward added.");
        }
        else if (collision.gameObject.layer == LayerMask.NameToLayer("MedianBoundary"))
        {
            // Reward for exiting the median boundary
            AddReward(0.1f);
            LogDebug("Agent left the median boundary. Reward added.");
        }
        else
        {
            // Penalize for any other unspecified collision
            AddReward(-0.1f);
            LogDebug("Agent penalized for being off-road or unspecified collision.");
        }

        LogDebug("TrafficAgent::OnCollisionExit completed successfully.");
    }

    /// <summary>
    /// Handles the event when the agent's Collider first enters another Collider's trigger zone.
    ///
    /// This method specifically manages interactions with objects tagged as "RoadBoundary":
    /// - Applies an immediate penalty (offRoadPenalty) when the agent enters a road boundary area.
    /// - Logs detailed information about the triggered object for debugging purposes.
    ///
    /// Key components:
    /// - offRoadPenalty: The reward adjustment applied for entering off-road areas.
    /// - AddReward: Method assumed to be part of a reinforcement learning framework (e.g., ML-Agents).
    ///
    /// Usage:
    /// This method is automatically called by Unity's physics system when the agent's Collider
    /// enters a trigger Collider. No manual invocation is needed.
    ///
    /// Note:
    /// - Ensure that road boundary objects are properly tagged as "RoadBoundary" in the Unity editor.
    /// - The logging provides detailed information about the triggered object, which can be
    ///   valuable for debugging and understanding the agent's interactions.
    /// - Adjust offRoadPenalty as needed to appropriately influence the agent's learning process.
    /// </summary>
    /// <param name="other">The Collider that the agent has entered. This Collider must be set as a trigger.</param>
    private void OnTriggerEnter(Collider other)
    {
        LogDebug("TrafficAgent::OnTriggerEnter started.");

        LogDebug($"Trigger entered with {other.gameObject.name}, tag: {other.gameObject.tag}, layer: {LayerMask.LayerToName(other.gameObject.layer)}");

        // Check the tag of the object we triggered
        if (other.gameObject.CompareTag("RoadBoundary"))
        {
            // Penalize the agent for going off the road
            AddReward(offRoadPenalty);
            LogDebug($"Entered road boundary trigger! Penalty added: {offRoadPenalty}");
        }

        /*
         * This method is called when a collider enters the trigger zone of another collider set as a trigger.
         * Use cases:
         *   - Detecting when an agent enters a specific area
         *   - Activating events when an agent reaches a checkpoint
         *   - Collecting items or power-ups
         */
        // Implementation here
        /*
        if (other.CompareTag("Agent"))
        {
            // Agent entered a checkpoint
            AddReward(1.0f);
            LogDebug("Agent reached checkpoint!");
        }
        else if (other.CompareTag("Collectible"))
        {
            // Agent collected an item
            AddReward(0.5f);
            Destroy(other.gameObject);
            LogDebug("Item collected!");
        }
        */

        LogDebug("TrafficAgent::OnTriggerEnter completed successfully.");
    }

    /// <summary>
    /// Handles continuous interaction between the agent's Collider and another Collider's trigger.
    ///
    /// This method is called every fixed frame-rate frame while the agent remains within a trigger zone.
    /// It specifically manages the agent's interaction with "RoadBoundary" tagged objects:
    ///
    /// - Applies a periodic penalty (offRoadPenalty) when the agent is within a road boundary area.
    /// - Uses a time-based interval (penaltyInterval) to control the frequency of penalty application.
    /// - Updates the lastPenaltyTime to track when the last penalty was applied.
    ///
    /// Key components:
    /// - offRoadPenalty: The reward adjustment applied for being off-road.
    /// - penaltyInterval: The minimum time between penalty applications.
    /// - lastPenaltyTime: Tracks the time of the last applied penalty.
    ///
    /// Usage:
    /// This method is automatically called by Unity's physics system. No manual invocation is needed.
    ///
    /// Note:
    /// - Ensure road boundary objects are tagged as "RoadBoundary" in the Unity editor.
    /// - The AddReward method is assumed to be part of a reinforcement learning framework (e.g., ML-Agents).
    /// - Adjust offRoadPenalty and penaltyInterval as needed for desired agent behavior.
    /// </summary>
    /// <param name="other">The Collider that the agent is continuously interacting with. This Collider must be set as a trigger.</param>
    private void OnTriggerStay(Collider other)
    {
        LogDebug("TrafficAgent::OnTriggerStay started.");

        /*
         * This method is called every fixed frame-rate frame while a collider remains inside the trigger zone.
         * Use cases:
         *   - Applying continuous effects while an agent is in an area
         *   - Monitoring time spent in a specific zone
         *   - Gradually increasing or decreasing a value based on presence in an area
         */
        // Implementation here
        if (other.gameObject.CompareTag("RoadBoundary"))
        {
            // Check if enough time has passed since the last penalty
            if (Time.time - lastPenaltyTime >= penaltyInterval)
            {
                // Apply the penalty
                AddReward(offRoadPenalty);
                lastPenaltyTime = Time.time;

                LogDebug($"Staying in road boundary! Penalty added: {offRoadPenalty}");
            }
        }

        LogDebug("TrafficAgent::OnTriggerStay completed successfully.");
    }

    /// <summary>
    /// Handles the event when the agent's Collider stops touching another Collider's trigger.
    ///
    /// This method specifically checks for interactions with objects tagged as "RoadBoundary":
    /// - When the agent exits a road boundary area, it resets the lastPenaltyTime to 0.
    /// - This reset allows for new penalties to be applied if the agent re-enters a boundary area.
    ///
    /// Usage:
    /// This method is automatically called by Unity's physics system when the
    /// agent's Collider exits a trigger Collider. No manual invocation is needed.
    ///
    /// Note:
    /// - Ensure that road boundary objects are properly tagged as "RoadBoundary" in the Unity editor.
    /// - The lastPenaltyTime variable should be defined elsewhere in the class.
    /// </summary>
    /// <param name="other">The Collider that the agent has stopped touching. This Collider must be set as a trigger.</param>
    private void OnTriggerExit(Collider other)
    {
        LogDebug("TrafficAgent::OnTriggerExit started.");

        /*
         * This method is called when a collider exits the trigger zone.
         * Use cases:
         *    - Deactivating effects when an agent leaves an area
         *    - Applying final rewards or penalties for leaving a zone
         *    - Resetting states or counters when an agent exits an area
         */
        // Implementation here
        if (other.gameObject.CompareTag("RoadBoundary"))
        {
            // Reset the last penalty time
            lastPenaltyTime = 0f;

            LogDebug("Exited road boundary area");
        }

        LogDebug("TrafficAgent::OnTriggerExit completed successfully.");
    }

    /// <summary>
    /// Generates random values for the agent's low-level actions within predefined ranges.
    ///
    /// This method sets three action values in the lowLevelActions array:
    /// 1. Index 0 - Steering angle: Range [-0.785398, 0.785398] radians (approximately ±45 degrees)
    /// 2. Index 1 - Acceleration: Range [0.0, 5.0]
    /// 3. Index 2 - Braking: Range [-5.0, 0.0]
    ///
    /// These randomized values can be used to:
    /// - Initialize the agent's actions at the start of an episode
    /// - Generate exploratory actions during training
    /// - Create diverse scenarios for testing or simulation
    ///
    /// Note: The ranges are hardcoded and may need adjustment based on the specific
    /// requirements of the traffic simulation or learning task.
    /// </summary>
    void GetRandomActions()
    {
        LogDebug("TrafficAgent::GetRandomActions started.");

        // Randomize the high-level action
        highLevelActions = Random.Range(0, 5); // Range is [0, 5)

        float minAngleRad = -Mathf.PI / 4f; // -45 degrees in radians (-0.785398...)
        float maxAngleRad = Mathf.PI / 4f;  // 45 degrees in radians (0.785398...)

        // Randomize the low-level actions
        lowLevelActions[0] = UnityEngine.Random.Range(minAngleRad, maxAngleRad); // Default value for steering
        lowLevelActions[1] = UnityEngine.Random.Range(0.0f, 5.0f); // Default value for acceleration
        lowLevelActions[2] = UnityEngine.Random.Range(-5.0f, 0.0f); // Default value for braking

        LogDebug("TrafficAgent::GetRandomActions completed successfully.");
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

    /// <summary>
    /// Appends a debug message to a log file in the application's data directory.
    ///
    /// This method:
    /// 1. Constructs the full path to the log file named "debug_log.txt" in the application's data folder.
    /// 2. Appends the provided message to the file, followed by a newline character.
    ///
    /// Usage:
    ///     LogToFile("Debug message here");
    ///
    /// Note: This method will create the file if it doesn't exist, or append to it if it does.
    /// Be aware that frequent writes to disk may impact performance in a production environment.
    ///
    /// </summary>
    /// <param name="message">The debug message to be logged to the file.</param>
    private void LogToFile(string message)
    {
        string path = Application.dataPath + "/debug_log.txt";
        System.IO.File.AppendAllText(path, message + "\n");
    }
}

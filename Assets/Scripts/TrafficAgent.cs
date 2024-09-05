using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;


public class TrafficAgent : Agent
{
    public int[] highLevelActions;
    public float[] lowLevelActions;

    [SerializeField]
    private Color hitColor = Color.red;

    [SerializeField]
    private Color missColor = Color.green;

    [SerializeField]
    private bool debugVisualization = false;

    // Properties
    private TrafficManager trafficManager;
    private float AngleStep;

    private Rigidbody rb;
    private bool isGrounded;
    public float moveSpeed = 5f;


    // Awake is called when the script instance is being loaded
    /*
     * This is called when the script instance is being loaded, before any Start() method is called.
     * It is often used for initializing references to other objects that are already present in the scene.
     */
    private void Awake()
    {
        Debug.Log("=== TrafficAgent::Awake Start START ===");
        if (trafficManager == null)
        {
            trafficManager = FindObjectOfType<TrafficManager>();

            if (trafficManager == null)
            {
                Debug.LogError("TrafficManager not found in the scene. Please add a TrafficManager to the scene.");
                return;
            }
        }

        AngleStep = trafficManager.raycastAngle / (trafficManager.numberOfRays - 1);


        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Assign to "Agent" layer (create this layer in Unity)
        gameObject.layer = LayerMask.NameToLayer("Road");

        Debug.Log("=== TrafficAgent::Awake END ===");
    }

    // Initialize is part of the ML-Agents specific setup
    // Initialize the agent and the traffic simulation
    /*
     * This is an ML-Agents-specific method that is used to initialize the agent.
     * It is called once when the agent is first created.
     * This is a good place to initialize variables and set up the environment specific to the ML-Agents framework.
     */
    public override void Initialize()
    {
        Debug.Log("=== TrafficAgent::Initialize START ===");

        base.Initialize();

        // Get the initial agent positions and create agent instances
        Debug.Log($"TrafficAgent Initialize called on {gameObject.name}");
        Debug.Log("=== TrafficAgent::Initialize END ===");
    }

    // Modify other methods to use trafficManager as needed
    /*
     *  If you want to reset or randomize agent positions at the start of each episode,
     *  this is the ideal place to do so. It ensures that the agents start each episode in a fresh state,
     *  which is often a requirement in reinforcement learning to provide diverse training experiences.
     */
    public override void OnEpisodeBegin()
    {
        Debug.Log("=== OnEpisodeBegin START ===");
        base.OnEpisodeBegin();

        if (trafficManager.agentPrefab == null)
        {
            Debug.LogError("agentPrefab is null at the end of OnEpisodeBegin");
            return;
        }

        IntPtr vehiclePtrVectorHandle = TrafficManager.Traffic_get_agents(trafficManager.trafficSimulationPtr);

        if (vehiclePtrVectorHandle == IntPtr.Zero)
        {
            Debug.LogError("Failed to get vehicle vector handle");
            return;
        }

        int vectorSize = TrafficManager.VehiclePtrVector_size(vehiclePtrVectorHandle);
        int indexValue = 0;

        // Loop through existing agents and respawn them at new random locations
        foreach (var agent in trafficManager.agentInstances.Values)
        {
            if (agent != null && indexValue < vectorSize)
            {
                // Generate new random positions
                float x = UnityEngine.Random.Range(-10f, 10f); // Example range, adjust as needed
                float y = 0f; // Assuming Y is constant, adjust as needed
                float z = UnityEngine.Random.Range(-50f, 50f); // Example range, adjust as needed

                IntPtr vehiclePtr = TrafficManager.VehiclePtrVector_get(vehiclePtrVectorHandle, indexValue);

                // Set new position using the provided Vehicle_setX, Y, Z functions
                if (vehiclePtr != IntPtr.Zero)
                {
                    TrafficManager.Vehicle_setX(vehiclePtr, x);
                    TrafficManager.Vehicle_setY(vehiclePtr, y);
                    TrafficManager.Vehicle_setZ(vehiclePtr, z);

                    // Update Unity GameObject position
                    agent.transform.position = new Vector3(x, y, z);

                    // Optionally reset agent state if needed, e.g., velocity, rotation, etc.
                }
                else
                {
                    Debug.LogWarning($"Failed to retrieve vehicle at index {indexValue}");
                }
                indexValue++;
            }
            Debug.Log($"Repositioned agents. Count: {indexValue}");
        }
        Debug.Log($"Created agents. agentInstances count: {trafficManager.agentInstances.Count}, agentColliders count: {trafficManager.agentColliders.Count}");

        // Ensure all agents are properly initialized with their new positions
        //trafficManager.UpdateAgentPositions();

        Debug.Log("=== OnEpisodeBegin END ===");
    }

    // Collect observations from the environment
    public override void CollectObservations(VectorSensor sensor)
    {
        Debug.Log("=== CollectObservations Start ===");

        Debug.Log($"CollectObservations called. trafficManager null? {trafficManager == null}");
        Debug.Log($"trafficManager.agentColliders null? {trafficManager.agentColliders == null}");
        Debug.Log($"trafficManager.agentColliders count: {trafficManager.agentColliders?.Count ?? 0}");
        Debug.Log($"Number of agent colliders: {trafficManager.agentColliders.Count}");
        Debug.Log($"Collected {sensor.ObservationSize()} observations");

        if (trafficManager == null)
        {
            Debug.LogError("TrafficManager not properly initialized");
            return;
        }
        Debug.Log($"Number of agent colliders: {trafficManager.agentColliders.Count}");

        if (trafficManager.agentColliders.Count == 0)
        {
            Debug.LogWarning("No agent colliders available for observations.");
            return;
        }

        Collider agentCollider = GetComponent<Collider>();
        if (agentCollider == null)
        {
            Debug.LogError("Agent colliders not properly initialized");
            return;
        }

        Vector3 rayStart = GetRayStartPosition(agentCollider);

        for (int i = 0; i < trafficManager.numberOfRays; i++)
        {
            float angle = trafficManager.raycastAngle / (trafficManager.numberOfRays - 1) * i;
            Vector3 direction = transform.TransformDirection(Quaternion.Euler(0, angle - trafficManager.raycastAngle / 2, 0) * Vector3.forward);

            if (Physics.Raycast(rayStart, direction, out RaycastHit hit, trafficManager.rayLength))
            {
                sensor.AddObservation(hit.distance / trafficManager.rayLength);
            }
            else
            {
                sensor.AddObservation(1.0f); // Normalized max distance for missed raycasts
            }
        }

        // Add agent's position and rotation as observations
        sensor.AddObservation(transform.position);
        //sensor.AddObservation(transform.rotation.eulerAngles.y);
        sensor.AddObservation(transform.rotation);

        Debug.Log("=== CollectObservations End ===");
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        Debug.Log("-- TrafficAgent::Heuristic called --");

        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        // Continuous actions
        continuousActions[0] = 0.0f; // Steering
        continuousActions[1] = 4.0f; // Acceleration
        continuousActions[2] = -1.5f; //Braking

        // Discrete actions
        discreteActions[0] = 0; // Default to 0

        Debug.LogError("Heuristic method called. Continuous Actions: " +
                  string.Join(", ", continuousActions) +
                  " Discrete Actions: " + string.Join(", ", discreteActions));

        //Rigidbody rb = GetComponent<Rigidbody>();
        //sensor.AddObservation(rb.velocity);

        //Debug.Log($"Observations: Position = {transform.position}, Velocity = {rb.velocity}");
    }

    // Execute the actions decided by the ML model
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        Debug.Log("-- TrafficAgent::OnActionReceived --");
        Debug.Log($"Discrete actions: {string.Join(", ", actionBuffers.DiscreteActions)}");
        Debug.Log($"Continuous actions: {string.Join(", ", actionBuffers.ContinuousActions)}");

        // Process Discrete (High-Level) Actions
        int highLevelActionCount = actionBuffers.DiscreteActions.Length;
        highLevelActions = new int[highLevelActionCount];

        for (int i = 0; i < highLevelActionCount; i++)
        {
            highLevelActions[i] = actionBuffers.DiscreteActions[i];
            Debug.Log($"HighLevelAction[{i}]: {highLevelActions[i]}");
        }

        // Process Continuous (Low-Level) Actions
        int lowLevelActionCount = actionBuffers.ContinuousActions.Length;
        lowLevelActions = new float[lowLevelActionCount];

        for (int i = 0; i < lowLevelActionCount; i++)
        {
            lowLevelActions[i] = actionBuffers.ContinuousActions[i];
            Debug.Log($"LowLevelAction[{i}]: {lowLevelActions[i]}");
        }

        // Step the simulation with the received actions
        TrafficManager.Traffic_step(trafficManager.trafficSimulationPtr, highLevelActions, lowLevelActions);

        // Update agent positions based on the simulation step
        //trafficManager.UpdateAgentPositions();  // This line is commented out

        // Calculate reward and determine if the episode should end
        float reward = CalculateReward();  // Implement your actual reward calculation logic
        bool done = CheckIfEpisodeIsDone();  // Implement your actual done condition logic

        SetReward(reward);
        /*
        if (done)
        {
            EndEpisode();
        }
        */
    }

    // Method used for handling tasks that need to be executed in sync with the frame rate
    private void Update()
    {
    /*
     * In Unity, the Update() method is called once per frame and is primarily used for handling tasks
     * that need to be executed in sync with the frame rate, such as processing user input,
     * updating non-physics game logic, and rendering-related updates.
     */
        Debug.Log("-- TrafficAgent::Update called --");

        // Existing Update logic
        if (isGrounded)
        {
            // Set veriticle axis position (y-axis) to zero to not go below road geometry
            // Actually change the agent's position
            Vector3 position = new Vector3(this.transform.position.x, 0.0f, this.transform.position.z);
            this.transform.position = position;
            rb.MovePosition(this.transform.position);
            isGrounded = false;
            Debug.Log($"isGrounded GameObject Position: X={this.transform.position.x:F2}, Y={this.transform.position.y:F2}, Z={this.transform.position.z:F2}");
        }
        else
        {
            rb.MovePosition(this.transform.position);
            Debug.Log($"GameObject Position: X={this.transform.position.x:F2}, Y={this.transform.position.y:F2}, Z={this.transform.position.z:F2}");

        }
        // Only draw debug rays if visualization is enabled
        if (debugVisualization || trafficManager.debugVisualization)
        {
            DrawDebugRays();
        }
    }

    private void DrawDebugRays()
    {
        if (trafficManager == null)
        {
            Debug.LogError("TrafficManager is null in DrawDebugRays");
            return;
        }

        Collider agentCollider = GetComponent<Collider>();
        if (agentCollider == null)
        {
            Debug.LogError("Agent Collider is null in DrawDebugRays");
            return;
        }

        Vector3 rayStart = GetRayStartPosition(agentCollider);

        float delta_angle = trafficManager.raycastAngle / (trafficManager.numberOfRays - 1);

        for (int i = 0; i < trafficManager.numberOfRays; i++)
        {
            float angle = delta_angle * i;
            Vector3 direction = transform.TransformDirection(Quaternion.Euler(0, angle - trafficManager.raycastAngle / 2, 0) * Vector3.forward);

            if (Physics.Raycast(rayStart, direction, out RaycastHit hit, trafficManager.rayLength))
            {
                Debug.DrawRay(rayStart, direction * hit.distance, hitColor, 0.0f);
                Debug.Log($"Ray {i} hit at distance: {hit.distance}");
            }
            else
            {
                Debug.DrawRay(rayStart, direction * trafficManager.rayLength, missColor, 0.0f);
                Debug.Log($"Ray {i} did not hit");
            }
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

    // Helper method to get the ray start position (center of the bounding box)
    private Vector3 GetRayStartPosition(Collider collider)
    {
        return collider.bounds.center + collider.transform.up * (collider.bounds.size.y / 2);
    }

    // Clean up the simulation on destroy
    private void OnDestroy()
    {
        Debug.Log("-- TrafficAgent::OnDestroy --");

        if (trafficManager != null)
        {
            if (trafficManager.agentInstances != null && trafficManager.agentInstances.ContainsKey(gameObject.name))
            {
                trafficManager.agentInstances.Remove(gameObject.name);
            }
            if (trafficManager.agentColliders != null && trafficManager.agentColliders.ContainsKey(gameObject.name))
            {
                trafficManager.agentColliders.Remove(gameObject.name);
            }
        }

        // Remove DecisionRequester first
        var decisionRequester = GetComponent<DecisionRequester>();
        if (decisionRequester != null)
        {
            Destroy(decisionRequester);
        }
    }

    void OnCollisionStay(Collision collision)
    {
        // Check if we're colliding with the road
        if (collision.gameObject.layer == LayerMask.NameToLayer("Road"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        // Check if we've left the road
        if (collision.gameObject.layer == LayerMask.NameToLayer("Road"))
        {
            isGrounded = false;
        }
    }
}

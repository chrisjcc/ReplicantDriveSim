using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Linq;


// Responsible for ML-Agents specific behaviors (collecting observations, receiving actions, etc.)
public class TrafficAgent : Agent
{
    // Properties
    [HideInInspector]
    private TrafficManager trafficManager;

    [HideInInspector]
    public int highLevelActions;

    //[HideInInspector]
    public float[] lowLevelActions;

    [SerializeField]
    private Color hitColor = Color.red;

    [SerializeField]
    private Color missColor = Color.green;

    [SerializeField]
    private bool debugVisualization = false;

    [HideInInspector]
    private bool isGrounded;
    //private Rigidbody rb;

    /*
    public float positiveReward = 1.0f;
    public float negativeReward = -0.5f;
    */

    // Awake is called when the script instance is being loaded
    /*
     * This is called when the script instance is being loaded, before any Start() method is called.
     * It is often used for initializing references to other objects that are already present in the scene.
     */
    private void Awake()
    {
        #if UNITY_EDITOR
        Debug.Log("--- TrafficAgent::Awake Start START ---");
        #endif

        if (trafficManager == null)
        {
            trafficManager = FindObjectOfType<TrafficManager>();

            if (trafficManager == null)
            {
                Debug.LogError("TrafficManager not found in the scene. Please add a TrafficManager to the scene.");
                return;
            }
            else
            {
                Debug.Log("TrafficManager found successfully.");
            }
        }

        /*
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Assign to "Agent" layer (create this layer in Unity)
        gameObject.layer = LayerMask.NameToLayer("Road");
        */

        float minAngleRad = -0.610865f; // -35 degrees in radians
        float maxAngleRad = 0.610865f;  // 35 degrees in radians

        lowLevelActions[0] = UnityEngine.Random.Range(minAngleRad, maxAngleRad); // Default value for steering
        lowLevelActions[1] = UnityEngine.Random.Range(0.0f, 4.5f); // Default value for acceleration
        lowLevelActions[2] = UnityEngine.Random.Range(-4.0f, 0.0f); // Default value for braking

        Debug.Log("--- TrafficAgent::Awake END ---");
    }

    // Initialize is part of the ML-Agents specific setup details
    // Initialize the agent and the traffic simulation
    /*
     * This is an ML-Agents-specific method that is used to initialize the agent.
     * It is called once when the agent is first created.
     * This is a good place to initialize variables and set up the environment specific to the ML-Agents framework.
     */
    public override void Initialize()
    {
        Debug.Log("--- TrafficAgent::Initialize START ---");

        #if UNITY_EDITOR
        // Get the initial agent positions and create agent instances
        Debug.Log($"TrafficAgent Initialize called on {gameObject.name}");
        #endif

        // Initialize your agent-specific variables here
        base.Initialize();


        Debug.Log("--- TrafficAgent::Initialize END ---");

    }

    // Modify other methods to use trafficManager as needed
    /*
     *  If you want to reset or randomize agent positions at the start of each episode,
     *  this is the ideal place to do so. It ensures that the agents start each episode in a fresh state,
     *  which is often a requirement in reinforcement learning to provide diverse training experiences.
     */
    public override void OnEpisodeBegin()
    {
        #if UNITY_EDITOR
        Debug.Log("--- OnEpisodeBegin START ---");
        #endif

        base.OnEpisodeBegin();

        if (trafficManager.agentPrefab == null)
        {
            Debug.LogError("agentPrefab is null at the end of OnEpisodeBegin");
            return;
        }

        float minAngleRad = -0.610865f; // -35 degrees in radians
        float maxAngleRad = 0.610865f;  // 35 degrees in radians

        lowLevelActions[0] = UnityEngine.Random.Range(minAngleRad, maxAngleRad); // Default value for steering
        lowLevelActions[1] = UnityEngine.Random.Range(0.0f, 4.5f); // Default value for acceleration
        lowLevelActions[2] = UnityEngine.Random.Range(-4.0f, 0.0f); // Default value for braking

        /*
        IntPtr vehiclePtrVectorHandle = TrafficManager.Traffic_get_agents(trafficManager.trafficSimulationPtr);

        if (vehiclePtrVectorHandle == IntPtr.Zero)
        {
            Debug.LogError("Failed to get vehicle vector handle");
            return;
        }

        //int vectorSize = TrafficManager.VehiclePtrVector_size(vehiclePtrVectorHandle);
        var agentValues = trafficManager.agentInstances.Values;
        int indexValue = 0;

        // Loop through existing agents and respawn them at new random locations
        foreach (var agent in agentValues)
        {
            //if (agent != null && indexValue < vectorSize)
            if (agent != null)
            {
                var vehiclePtr = TrafficManager.VehiclePtrVector_get(vehiclePtrVectorHandle, indexValue);

                // Generate new random positions
                //float x = TrafficManager.Vehicle_getX(vehiclePtr); // UnityEngine.Random.Range(-10f, 10f); // Example range, adjust as needed
                //float y = TrafficManager.Vehicle_getY(vehiclePtr); // 0f; // Assuming Y is constant, adjust as needed
                //float z = TrafficManager.Vehicle_getZ(vehiclePtr); // UnityEngine.Random.Range(-50f, 50f); // Example range, adjust as needed

                // Set new position using the provided Vehicle_setX, Y, Z functions

                if (vehiclePtr != IntPtr.Zero)
                {
                    //TrafficManager.Vehicle_setX(vehiclePtr, x);
                    Debug.Log($"X-position: {TrafficManager.Vehicle_getX(vehiclePtr)}");
                    //TrafficManager.Vehicle_setY(vehiclePtr, y);
                    Debug.Log($"Y-position: {TrafficManager.Vehicle_getY(vehiclePtr)}");
                    //TrafficManager.Vehicle_setZ(vehiclePtr, z);
                    Debug.Log($"Z-position: {TrafficManager.Vehicle_getZ(vehiclePtr)}");

                    // Update Unity GameObject position
                    //agent.transform.position = new Vector3(x, y, z);

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
        */
        Debug.Log($"Created agents. agentInstances count: {trafficManager.agentInstances.Count}, agentColliders count: {trafficManager.agentColliders.Count}");

        Debug.Log("--- OnEpisodeBegin END ---");
    }

    // Collect observations from the environment
    public override void CollectObservations(VectorSensor sensor)
    {
        #if UNITY_EDITOR
        Debug.Log("--- CollectObservations Start ---");

        Debug.Log($"CollectObservations called. trafficManager null? {trafficManager == null}");
        Debug.Log($"trafficManager.agentColliders null? {trafficManager.agentColliders == null}");
        Debug.Log($"trafficManager.agentColliders count: {trafficManager.agentColliders?.Count ?? 0}");
        Debug.Log($"Number of agent colliders: {trafficManager.agentColliders.Count}");
        Debug.Log($"Collected {sensor.ObservationSize()} observations");
        #endif

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

        Debug.Log($"{transform.rotation.eulerAngles}");

        for (int i = 0; i < trafficManager.numberOfRays; i++)
        {
            float angle = trafficManager.AngleStep * i;
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

        Debug.Log($"CollectObservations: Euler angles: Pitch={transform.rotation.eulerAngles.x}, Yaw={transform.rotation.eulerAngles.y}, Roll={transform.rotation.eulerAngles.z}");

        // Add agent's position and rotation as observations
        sensor.AddObservation(transform.position);
        //sensor.AddObservation(transform.rotation);
        //sensor.AddObservation(transform.rotation.eulerAngles);
        sensor.AddObservation(transform.rotation.eulerAngles.y);

        //Rigidbody rb = GetComponent<Rigidbody>();
        //sensor.AddObservation(rb.velocity);

        //Debug.Log($"Observations: Position = {transform.position}, Velocity = {rb.velocity}");

        Debug.Log("--- CollectObservations End ---");
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Debug logs to check method calls
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::Heuristic START --");
        #endif

        // Get discrete actions array from ActionBuffers
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = UnityEngine.Random.Range(0, 5); // This will give a random integer from 0 to 4 inclusive

        float minAngleRad = -0.610865f; // -35 degrees in radians
        float maxAngleRad = 0.610865f;  // 35 degrees in radians

        // For continuous actions, assuming index 0 is steering and 1 is acceleration and 2 is braking:
        var continuousActions = actionsOut.ContinuousActions;

        // Sample a random angle between -35 and 35 degrees and convert to radians for steering
        continuousActions[0] = UnityEngine.Random.Range(minAngleRad, maxAngleRad); // Steering

        // Sample a random value for acceleration
        continuousActions[1] = UnityEngine.Random.Range(0.0f, 4.5f); // Acceleration

        // Sample a random value for braking
        continuousActions[2] = UnityEngine.Random.Range(-4.0f, 0.0f); // Braking

        Debug.Log("Heuristic method called. Discrete Actions: " +
                  string.Join(", ", discreteActions) +
                  " Continuous Actions: " + string.Join(", ", continuousActions));

        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::Heuristic END --");
        #endif
    }

    // Execute the actions decided by the ML model
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::OnActionReceived --");

        Debug.Log($"Discrete actions: {string.Join(", ", actionBuffers.DiscreteActions)}");
        Debug.Log($"Continuous actions: {string.Join(", ", actionBuffers.ContinuousActions)}");
        #endif

        // Process Discrete (High-Level) Actions
        int highLevelActionCount = actionBuffers.DiscreteActions.Length;
        highLevelActions = actionBuffers.DiscreteActions[0];

        // Process Continuous (Low-Level) Actions
        int lowLevelActionCount = actionBuffers.ContinuousActions.Length;

        for (int i = 0; i < lowLevelActionCount; i++)
        {
            // Fill the arrays with data from actionBuffers
            lowLevelActions[i] = actionBuffers.ContinuousActions[i];
        }

        Debug.Log($"High-level actions: {string.Join(", ", highLevelActions)}");
        Debug.Log($"Low-level actions: {string.Join(", ", lowLevelActions)}");

        // Calculate reward and determine if the episode should end
        float reward = CalculateReward();  // Implement your actual reward calculation logic
        bool done = CheckIfEpisodeIsDone();  // Implement your actual done condition logic

        //SetReward(reward); // To set the reward to a specific value at a particular point in time.
        AddReward(reward); // To incrementally build up the reward over time

        /*
        if (done)
        {
            EndEpisode();
        }
        */

        // Move this to FixedUpdate
        // rb.MovePosition(transform.position + transform.forward * lowLevelActions[1] * Time.fixedDeltaTime);
        // transform.Rotate(Vector3.up, lowLevelActions[0] * Time.fixedDeltaTime);

        Debug.Log("-- OnActionReceived END --");
    }

    // This method is only for visualization and doesn't affect agent movement.
    //private void Update()
    private void FixedUpdate()
    {
        /*
         * In Unity, the Update() method is called once per frame and is primarily used for handling tasks
         * that need to be executed in sync with the frame rate, such as processing user input,
         * updating non-physics game logic, and rendering-related updates.
         */
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::FixedUpdate START --");
        #endif

        // Only draw debug rays if visualization is enabled
        if (debugVisualization || trafficManager.debugVisualization)
        {
            DrawDebugRays();
        }
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::FixedUpdate END --");
        #endif
    }

    void Update()
    {
        Debug.Log($"TrafficAgent::Update: Agent {gameObject.name} rotation: {transform.rotation.eulerAngles}");
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

    /*
    private void OnCollisionEnter(Collision collision)
    {
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::OnCollisionEnter --");
        #endif

        // Check the tag of the object we collided with
        switch (collision.gameObject.tag)
        {
            case "Goal":
                // Reward the agent for reaching the goal
                AddReward(positiveReward);
                Debug.Log("Goal reached! Reward added: " + positiveReward);
                EndEpisode();
                break;

            case "Obstacle":
                // Penalize the agent for hitting an obstacle
                AddReward(negativeReward);
                Debug.Log("Hit obstacle! Penalty added: " + negativeReward);
                break;

            case "Collectible":
                // Reward the agent for collecting an item
                AddReward(0.5f);
                Debug.Log("Item collected! Reward added: 0.5");
                // Optionally destroy the collectible
                Destroy(collision.gameObject);
                break;

            case "DeathZone":
                // End the episode if the agent enters a death zone
                AddReward(-1.0f);
                Debug.Log("Entered death zone! Episode ended with penalty: -1.0");
                EndEpisode();
                break;

            case "IsOnRoad":
                // Example reward based on staying on the road
                AddReward(0.1f); //-1.0f
                //EndEpisode();
                break;

            default:
                // For any other collision, we might want to add a small negative reward
                AddReward(-0.1f);
                Debug.Log("Unspecified collision! Small penalty added: -0.1");
                break;
        }
    }
    */

    // Clean up the simulation on destroy
    private void OnDestroy()
    {
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::OnDestroy --");
        #endif

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
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::OnCollisionStay --");
        #endif

        // Check if we're colliding with the road
        if (collision.gameObject.layer == LayerMask.NameToLayer("Road"))
        {
            isGrounded = true;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::OnCollisionExit --");
        #endif

        // Check if we've left the road
        if (collision.gameObject.layer == LayerMask.NameToLayer("Road"))
        {
            isGrounded = false;
        }
    }

    private void LogToFile(string message)
    {
        string path = Application.dataPath + "/debug_log.txt";
        System.IO.File.AppendAllText(path, message + "\n");
    }
}

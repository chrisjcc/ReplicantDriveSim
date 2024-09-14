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

    [HideInInspector]
    public float[] lowLevelActions;

    [SerializeField]
    private Color hitColor = Color.red;

    [SerializeField]
    private Color missColor = Color.green;

    [SerializeField]
    private bool debugVisualization = false;

    [HideInInspector]
    Guid channelId = new Guid("621f0a70-4f87-11ea-a6bf-784f4387d1f7");

    [HideInInspector]
    private bool isGrounded;

    /*
    private Rigidbody rb;
    public float positiveReward = 1.0f;
    public float negativeReward = -0.5f;
    */

    /// <summary>
    /// Awake is called when the script instance is being loaded.
    /// This is called when the script instance is being loaded, before any Start() method is called.
    /// It is often used for initializing references to other objects that are already present in the scene.
    /// </summary>
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

        // Initialize lowLevelActions array with appropriate size (e.g., 3 for steering, acceleration, and braking)
        lowLevelActions = new float[3];

        #if UNITY_EDITOR
        Debug.Log("--- TrafficAgent::Awake END ---");
        #endif
    }

    /// <summary>
    /// Initialize is part of the ML-Agents specific setup details.
    /// Initialize the agent and the traffic simulation.
    /// This is an ML-Agents-specific method that is used to initialize the agent.
    /// It is called once when the agent is first created.
    /// This is a good place to initialize variables and set up the environment specific to the ML-Agents framework.
    /// </summary>
    public override void Initialize()
    {
        #if UNITY_EDITOR
        Debug.Log("--- TrafficAgent::Initialize START ---");
        // Get the initial agent positions and create agent instances
        Debug.Log($"TrafficAgent Initialize called on {gameObject.name}");
        #endif

        // Initialize your agent-specific variables here
        base.Initialize();
        ResetActions();

        #if UNITY_EDITOR
        Debug.Log("--- TrafficAgent::Initialize END ---");
        #endif

    }

    /// <summary>
    ///  If you want to reset or randomize agent positions at the start of each episode,
    ///  this is the ideal place to do so.It ensures that the agents start each episode in a fresh state,
    ///  which is often a requirement in reinforcement learning to provide diverse training experiences.
    /// </summary>
    public override void OnEpisodeBegin()
    {
        #if UNITY_EDITOR
        Debug.Log("--- OnEpisodeBegin START ---");
        #endif

        base.OnEpisodeBegin();
        ResetActions();

        #if UNITY_EDITOR
        Debug.Log($"Created agents. agentInstances count: {trafficManager.agentInstances.Count}, agentColliders count: {trafficManager.agentColliders.Count}");

        Debug.Log("--- OnEpisodeBegin END ---");
        #endif
    }

    /// <summary>
    /// Randomly select action values
    /// </summary>
    private void ResetActions()
    {
        float minAngleRad = -0.610865f;
        float maxAngleRad = 0.610865f;
        lowLevelActions[0] = UnityEngine.Random.Range(minAngleRad, maxAngleRad);
        lowLevelActions[1] = UnityEngine.Random.Range(0.0f, 4.5f);
        lowLevelActions[2] = UnityEngine.Random.Range(-4.0f, 0.0f);
    }

    /// <summary>
    /// Collect observations from the environment
    /// </summary>
    /// <param name="sensor">Collect observations</param>
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

        if (trafficManager == null || !trafficManager.agentColliders.ContainsKey(gameObject.name))
        {
            Debug.LogWarning($"TrafficManager or agent collider not properly initialized for {gameObject.name}");
            return;
        }

        Collider agentCollider = trafficManager.agentColliders[gameObject.name];
        if (agentCollider == null)
        {
            Debug.LogError($"Agent collider not found for {gameObject.name}");
            return;
        }

        Vector3 rayStart = GetRayStartPosition(agentCollider);

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

    /// <summary>
    /// When Behavior Type is set to "Heuristic Only" on the agent's Behavior Parameters,
    /// this function will be called. Its return value will be fed into
    /// <see cref="OnActionReceived(float[])"/> instead of using the neural network
    /// </summary>
    /// <param name="actionsOut">An output action array</param>
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

    /// <summary>
    /// Execute the actions decided by the ML model
    /// </summary>
    /// <param name="actionBuffers">The actions to take</param>
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::OnActionReceived --");

        Debug.Log($"Discrete actions: {string.Join(", ", actionBuffers.DiscreteActions)}");
        Debug.Log($"Continuous actions: {string.Join(", ", actionBuffers.ContinuousActions)}");
        #endif

        // Process Discrete (High-Level) Actions
        highLevelActions = actionBuffers.DiscreteActions[0];

        // Process Continuous (Low-Level) Actions
        int lowLevelActionCount = actionBuffers.ContinuousActions.Length;

        for (int i = 0; i < lowLevelActionCount; i++)
        {
            // Fill the arrays with data from actionBuffers
            lowLevelActions[i] = actionBuffers.ContinuousActions[i];
        }

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

        #if UNITY_EDITOR
        Debug.Log("-- OnActionReceived END --");
        #endif
    }

    /// <summary>
    /// Called every .02 seconds.
    /// This method is only for visualization and doesn't affect agent movement.
    /// </summary>
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
        if (debugVisualization || (trafficManager != null && trafficManager.debugVisualization))
        {
            DrawDebugRays();
        }

        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::FixedUpdate END --");
        #endif
    }

    /// <summary>
    /// For debugging purposes
    /// </summary>
    void Update()
    {
        #if UNITY_EDITOR
        Debug.Log($"TrafficAgent::Update: Agent {gameObject.name} rotation: {transform.rotation.eulerAngles}");
        #endif
    }

    /// <summary>
    /// Draw Raycast in scene
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

    /// <summary>
    /// Calculate reward singal
    /// </summary>
    /// <returns></returns>
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

    /// <summary>
    /// Called when the agent collides with something solid
    /// </summary>
    /// <param name="collision">The collision info</param>
    private void OnCollisionEnter(Collision collision)
    {
        #if UNITY_EDITOR
        Debug.Log("-- TrafficAgent::OnCollisionEnter --");
        #endif

        /*
        // Check the tag of the object we collided with
        switch (collision.gameObject.tag) // collision.collider.CompareTag("boundary")
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
        */
    }

    void GetRandomActions()
    {
        float minAngleRad = -0.610865f; // -35 degrees in radians
        float maxAngleRad = 0.610865f;  // 35 degrees in radians

        lowLevelActions[0] = UnityEngine.Random.Range(minAngleRad, maxAngleRad); // Default value for steering
        lowLevelActions[1] = UnityEngine.Random.Range(0.0f, 4.5f); // Default value for acceleration
        lowLevelActions[2] = UnityEngine.Random.Range(-4.0f, 0.0f); // Default value for braking
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
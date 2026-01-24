using System;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

// P/Invoke-free TrafficAgent that works with TrafficManagerSafe
public class TrafficAgentSafe : MonoBehaviour
{
    [Header("Agent Properties")]
    public int agentId = -1;
    public float moveSpeed = 5f;
    public float rotationSpeed = 90f;

    [Header("Debug Visualization")]
    public bool showDebugInfo = true;
    public Color agentColor = Color.red;

    private TrafficManagerSafe trafficManager;
    private Vector3 targetPosition;
    private Vector3 velocity;
    private float yawAngle = 0f;

    // Simulation state management
    private bool useSimulationState = false;
    private Vector3 simulationPosition;
    private Vector3 simulationVelocity;
    private float simulationYaw;

    // Simple movement pattern for demonstration (fallback when no simulation)
    private float timeOffset;
    private float moveRadius = 15f;
    private Vector3 centerPoint;

    public void Initialize(int id, TrafficManagerSafe manager)
    {
        agentId = id;
        trafficManager = manager;
        timeOffset = id * 2f; // Offset each agent's movement pattern
        centerPoint = transform.position;

        // Set initial target position
        targetPosition = transform.position;

        Debug.Log($"TrafficAgentSafe: Agent {agentId} initialized at {transform.position}");
    }

    void Start()
    {
        if (agentId >= 0)
        {
            // Apply unique color based on agent ID
            ApplyAgentColor();

            // Start with a simple circular movement pattern
            InitializeMovementPattern();
        }
    }

    private void ApplyAgentColor()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = renderer.material;
            float hue = (float)agentId / (trafficManager != null ? trafficManager.numberOfAgents : 10f);
            agentColor = Color.HSVToRGB(hue, 0.8f, 0.9f);
            material.color = agentColor;
        }
    }

    private void InitializeMovementPattern()
    {
        // Create a circular movement pattern around the spawn point
        centerPoint = transform.position;

        // Spread agents out in a circle initially
        float angle = (float)agentId / (trafficManager != null ? trafficManager.numberOfAgents : 1f) * 2f * Mathf.PI;
        Vector3 offset = new Vector3(Mathf.Cos(angle) * moveRadius, 0, Mathf.Sin(angle) * moveRadius);
        targetPosition = centerPoint + offset;

        Debug.Log($"TrafficAgentSafe: Agent {agentId} movement initialized - center: {centerPoint}, target: {targetPosition}");
    }

    public void UpdateAgent(float deltaTime)
    {
        if (agentId < 0) return;

        if (useSimulationState)
        {
            // Use state from C++ traffic simulation
            ApplySimulationState();
        }
        else
        {
            // Wait for simulation state - no fallback movement
            Debug.LogWarning($"TrafficAgentSafe {agentId}: No simulation state received yet");
        }
    }

    private void UpdateMovementPattern(float deltaTime)
    {
        // Simple circular movement around center point
        float time = Time.time + timeOffset;
        float angle = time * 0.5f; // Speed of circular movement

        Vector3 newTarget = centerPoint + new Vector3(
            Mathf.Cos(angle) * moveRadius,
            0,
            Mathf.Sin(angle) * moveRadius
        );

        targetPosition = newTarget;
    }

    private void MoveTowardsTarget(float deltaTime)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        velocity = direction * moveSpeed;

        // Apply movement
        transform.position += velocity * deltaTime;

        // Keep agents at a reasonable height
        Vector3 pos = transform.position;
        pos.y = Mathf.Max(pos.y, 0.5f);
        transform.position = pos;
    }

    private void UpdateRotation(float deltaTime)
    {
        if (velocity.magnitude > 0.1f)
        {
            // Calculate target rotation based on velocity
            float targetYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;

            // Smoothly rotate towards target
            float yawDifference = Mathf.DeltaAngle(yawAngle, targetYaw);
            float maxRotation = rotationSpeed * deltaTime;

            if (Mathf.Abs(yawDifference) > maxRotation)
            {
                yawAngle += Mathf.Sign(yawDifference) * maxRotation;
            }
            else
            {
                yawAngle = targetYaw;
            }

            // Apply rotation
            transform.rotation = Quaternion.Euler(0, yawAngle, 0);
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebugInfo || agentId < 0) return;

        // Draw agent info
        Gizmos.color = agentColor;

        // Draw current position
        Gizmos.DrawWireSphere(transform.position, 1f);

        // Draw target position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(targetPosition, 0.5f);

        // Draw movement path
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, targetPosition);

        // Draw velocity vector
        if (velocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, velocity.normalized * 3f);
        }

        // Draw center point and movement radius
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(centerPoint, moveRadius);
    }

    void OnDrawGizmosSelected()
    {
        if (!showDebugInfo || agentId < 0) return;

        // Draw detailed debug info when selected
        Gizmos.color = Color.white;

        // Draw agent ID
        Vector3 labelPos = transform.position + Vector3.up * 3f;

#if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPos, $"Agent {agentId}");
        UnityEditor.Handles.Label(labelPos + Vector3.down * 0.5f, $"Speed: {velocity.magnitude:F1}");
        UnityEditor.Handles.Label(labelPos + Vector3.down * 1f, $"Yaw: {yawAngle:F1}°");
#endif
    }

    // Get agent state information
    public Vector3 GetPosition() { return transform.position; }
    public Vector3 GetVelocity() { return velocity; }
    public float GetYaw() { return yawAngle; }
    public int GetAgentId() { return agentId; }

    // Set agent properties
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
        centerPoint = position; // Update movement center
    }

    public void SetVelocity(Vector3 vel)
    {
        velocity = vel;
    }

    public void SetYaw(float yaw)
    {
        yawAngle = yaw;
        transform.rotation = Quaternion.Euler(0, yaw, 0);
    }

    public void SetMoveRadius(float radius)
    {
        moveRadius = Mathf.Max(radius, 1f);
    }

    public void SetCenterPoint(Vector3 center)
    {
        centerPoint = center;
    }

    // Simulation state methods
    public void SetSimulationState(Vector3 position, Vector3 velocity, float yaw)
    {
        simulationPosition = position;
        simulationVelocity = velocity;
        simulationYaw = yaw;
        useSimulationState = true;

        Debug.Log($"TrafficAgentSafe {agentId}: Using C++ simulation state - Pos={position}, Vel={velocity}, Yaw={yaw:F1}°");
    }

    private void ApplySimulationState()
    {
        // Apply position from C++ simulation
        Vector3 oldPosition = transform.position;
        transform.position = simulationPosition;

        // Update local velocity tracking
        velocity = simulationVelocity;

        // Apply rotation from C++ simulation
        yawAngle = simulationYaw;
        transform.rotation = Quaternion.Euler(0, yawAngle, 0);

        // Keep agents at reasonable height
        // Check if this is a vehicle prefab or a cube
        Vector3 pos = transform.position;
        bool isVehiclePrefab = GetComponent<MeshFilter>() == null || GetComponent<MeshFilter>().mesh.name != "Cube";

        if (isVehiclePrefab)
        {
            // Vehicle prefabs should stay at ground level
            pos.y = Mathf.Max(pos.y, 0f);
        }
        else
        {
            // Cubes need to be slightly raised
            pos.y = Mathf.Max(pos.y, 0.5f);
        }
        transform.position = pos;

        // Debug: Log actual transform update (very sensitive threshold)
        if (Vector3.Distance(oldPosition, transform.position) > 0.001f)
        {
            Debug.Log($"TrafficAgentSafe {agentId}: Transform updated from {oldPosition} to {transform.position}");
        }
        else if (Time.frameCount % 60 == 0) // Log every 60 frames even if no movement
        {
            Debug.Log($"TrafficAgentSafe {agentId}: No transform movement - Old: {oldPosition}, Sim: {simulationPosition}, Final: {transform.position}");
        }
    }

    // Logging methods
    public void LogAgentState()
    {
        Debug.Log($"TrafficAgentSafe {agentId}: Pos={transform.position}, Vel={velocity}, Yaw={yawAngle:F1}°, Target={targetPosition}");
    }

    // ML-Agents Integration Methods
    public void InitializeMLAgents()
    {
        var mlAgent = GetComponent<Agent>();
        if (mlAgent != null)
        {
            // ML-Agent will handle its own initialization
            Debug.Log($"TrafficAgentSafe {agentId}: ML-Agents component found and will be used for observations/actions");
        }
    }

    // Provide current traffic simulation state to ML-Agents if needed
    public Vector3 GetCurrentVelocity() { return velocity; }
    public float GetCurrentYaw() { return yawAngle; }
    public Vector3 GetTargetPosition() { return targetPosition; }
    public bool IsUsingSimulationState() { return useSimulationState; }
}
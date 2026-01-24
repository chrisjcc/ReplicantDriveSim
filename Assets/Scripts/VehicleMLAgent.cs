using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

// ML-Agents bridge for NISSAN-GTR vehicles in traffic simulation
public class VehicleMLAgent : Agent
{
    private TrafficAgentSafe trafficAgent;

    [Header("Vehicle ML Settings")]
    public float maxSpeed = 20f;
    public float maxAngularVelocity = 180f;

    [Header("Raycast Sensor Settings")]
    public float rayDistance = 100f;
    public int numRays = 12;
    public float rayAngleRange = 180f;
    public LayerMask detectableLayers = -1;

    private RayPerceptionSensorComponent3D raycastSensor;

    public override void Initialize()
    {
        // Try to find TrafficAgentSafe, but don't fail if it's not there yet
        // It will be added later by TrafficManagerSafe
        trafficAgent = GetComponent<TrafficAgentSafe>();
        if (trafficAgent == null)
        {
            Debug.Log($"VehicleMLAgent: TrafficAgentSafe not yet available on {gameObject.name}, will be set later");
        }
        else
        {
            Debug.Log($"VehicleMLAgent: Found TrafficAgentSafe on {gameObject.name}");
        }

        // Set up raycast sensor
        SetupRaycastSensor();
    }

    // Public method to set TrafficAgentSafe after it's added
    public void SetTrafficAgent(TrafficAgentSafe agent)
    {
        trafficAgent = agent;
        Debug.Log($"VehicleMLAgent: TrafficAgentSafe has been set on {gameObject.name}");
    }

    private void SetupRaycastSensor()
    {
        // Check if RayPerceptionSensorComponent3D already exists
        raycastSensor = GetComponent<RayPerceptionSensorComponent3D>();

        if (raycastSensor == null)
        {
            // Add raycast sensor component
            raycastSensor = gameObject.AddComponent<RayPerceptionSensorComponent3D>();
            Debug.Log($"VehicleMLAgent: Added RayPerceptionSensorComponent3D to {gameObject.name}");
        }

        // Configure the raycast sensor
        raycastSensor.SensorName = "VehicleRaycast";
        // Only use tags that exist - remove "Vehicle" until it's added to Unity Tag Manager
        raycastSensor.DetectableTags = new List<string> { "Untagged" };
        raycastSensor.RayLength = rayDistance;
        raycastSensor.RayLayerMask = detectableLayers;
        raycastSensor.ObservationStacks = 1;
        raycastSensor.RaysPerDirection = numRays / 2;
        raycastSensor.MaxRayDegrees = rayAngleRange / 2;
        raycastSensor.SphereCastRadius = 0.5f;
        raycastSensor.StartVerticalOffset = 2.5f;
        raycastSensor.EndVerticalOffset = 0.0f;

        Debug.Log($"VehicleMLAgent: Configured raycast sensor with {numRays} rays, {rayDistance}m range");
    }

    public override void OnEpisodeBegin()
    {
        // Reset episode if needed
        // For traffic simulation, we typically don't reset episodes
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (trafficAgent == null)
        {
            // If TrafficAgentSafe is not available yet, fill with zeros
            Debug.LogWarning($"VehicleMLAgent: TrafficAgentSafe not found on {gameObject.name}, filling observations with zeros");
            for (int i = 0; i < 8; i++) // Add 8 zero observations to match vector observation size
            {
                sensor.AddObservation(0f);
            }
            return;
        }

        // Add vehicle state observations
        Vector3 velocity = trafficAgent.GetCurrentVelocity();
        Vector3 position = transform.position;
        float yaw = trafficAgent.GetCurrentYaw();

        // Normalize observations
        sensor.AddObservation(position.x / 100f);  // Normalized position X
        sensor.AddObservation(position.z / 100f);  // Normalized position Z
        sensor.AddObservation(velocity.x / maxSpeed);  // Normalized velocity X
        sensor.AddObservation(velocity.z / maxSpeed);  // Normalized velocity Z
        sensor.AddObservation(yaw / 360f);  // Normalized rotation

        // Add target information if available
        Vector3 target = trafficAgent.GetTargetPosition();
        Vector3 dirToTarget = (target - position).normalized;
        sensor.AddObservation(dirToTarget.x);      // Direction to target X
        sensor.AddObservation(dirToTarget.z);      // Direction to target Z
        sensor.AddObservation(Vector3.Distance(position, target) / 50f);  // Normalized distance to target

        Debug.Log($"VehicleMLAgent {trafficAgent.GetAgentId()}: Collected 8 observations - Pos: {position}, Vel: {velocity}, Yaw: {yaw}");
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // For now, let the traffic simulation handle movement
        // ML-Agents actions could be used to modify traffic behavior in the future

        // The traffic simulation will continue to update the vehicle position
        // through TrafficAgentSafe.SetSimulationState()

        // Optional: Apply small modifications to the traffic simulation based on ML decisions
        // float steerInput = Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, 1f);
        // float speedInput = Mathf.Clamp(actionBuffers.ContinuousActions[1], -1f, 1f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Provide default/heuristic actions when no trainer is connected
        var continuousActionsOut = actionsOut.ContinuousActions;

        // Default behavior: go straight at moderate speed
        continuousActionsOut[0] = 0f;  // No steering
        continuousActionsOut[1] = 0.5f; // Half speed
    }

    // Helper methods for TrafficAgentSafe integration
    public void NotifyTrafficSimulationUpdate(Vector3 position, Vector3 velocity, float yaw)
    {
        // This can be called when the traffic simulation updates the vehicle
        // Can be used for reward calculation or other ML-Agents specific logic
    }
}
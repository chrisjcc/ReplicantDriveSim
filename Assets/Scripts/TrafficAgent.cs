using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class TrafficAgent : Agent
{
    // Import functions from the external TrafficSimulation library
    const string DllName = "ReplicantDriveSim"; //"TrafficSimulation";
    
    [DllImport(DllName)]
    private static extern IntPtr Traffic_create(int num_agents, uint seed);

    [DllImport(DllName)]
    private static extern void Traffic_destroy(IntPtr traffic);

    [DllImport(DllName)]
    private static extern void Traffic_step(IntPtr traffic, float[] high_level_actions, float[] low_level_actions, int num_actions);

    private IntPtr trafficSimulation;
    private float[] highLevelActions;
    private float[] lowLevelActions;

    public GameObject agentPrefab;  // Reference to the agent prefab (e.g., a car model such as Mercedes-Benz AMG GT-R)
    private Dictionary<string, GameObject> agentInstances = new Dictionary<string, GameObject>(); // Dictionary to store agent instances

    // Initialize the agent and the traffic simulation
    public override void Initialize()
    {
        base.Initialize();
        trafficSimulation = Traffic_create(10, 12345); // Create simulation with 10 agents and seed 12345
        highLevelActions = new float[10];
        lowLevelActions = new float[10];
    }

    // Collect observations from the environment
    public override void CollectObservations(VectorSensor sensor)
    {
        // Collect the state of the traffic simulation here
        // For example, sensor.AddObservation(position) or other relevant observations
        // (Assume the Traffic_step method or another method provides the state)
        // sensor.AddObservation(someSimulationState);
    }

    // Execute the actions decided by the ML model
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // Add the null checks before using or clearing the buffers
        if (actionBuffers.DiscreteActions.Array != null)
        {
            actionBuffers.DiscreteActions.Clear();
        }
        if (actionBuffers.ContinuousActions.Array != null)
        {
            actionBuffers.ContinuousActions.Clear();
        }

        // Convert the actions to arrays (assumes continuous actions)
        ActionSegment<float> continuousActions = actionBuffers.ContinuousActions;
        for (int i = 0; i < continuousActions.Length; i++)
        {
            highLevelActions[i] = continuousActions[i]; // Assuming high-level actions map directly
            lowLevelActions[i] = continuousActions[i];  // Adjust if low-level actions are different
        }

        // Step the simulation
        Traffic_step(trafficSimulation, highLevelActions, lowLevelActions, highLevelActions.Length);

        // Update the positions of the agents in Unity
        UpdateAgentPositions();

        // Collect reward and determine if episode is done
        float reward = 0f;  // Replace with actual reward logic
        bool done = false;  // Replace with actual done condition logic

        SetReward(reward);
        if (done)
        {
            EndEpisode();
        }
    }

    // Handle manual control, if needed
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Example: Set actions manually for debugging
        var continuousActionsOut = actionsOut.ContinuousActions;
        for (int i = 0; i < continuousActionsOut.Length; i++)
        {
            continuousActionsOut[i] = 0; // Replace with manual action values for debugging
        }
    }

    // Update the positions of agents based on the simulation results
    private void UpdateAgentPositions()
    {
        // Loop through all agents in the simulation and update their positions in Unity
        for (int i = 0; i < highLevelActions.Length; i++)
        {
            // Assuming the simulation provides the positions and orientations
            Vector3 position = new Vector3(highLevelActions[i], 0, lowLevelActions[i]); // Example conversion
            Quaternion rotation = Quaternion.Euler(0, highLevelActions[i] * 360, 0);  // Example conversion

            string agentID = i.ToString();  // Use the index as the agent ID

            // Create a new agent instance if it doesn't exist
            if (!agentInstances.ContainsKey(agentID))
            {
                GameObject newAgent = Instantiate(agentPrefab, position, rotation);
                agentInstances.Add(agentID, newAgent);
            }
            else // Update the position and rotation of the agent instance
            {
                agentInstances[agentID].transform.position = position;
                agentInstances[agentID].transform.rotation = rotation;
            }
        }
    }


    // Clean up the simulation on destroy
    void OnDestroy()
    {
        if (trafficSimulation != IntPtr.Zero)
        {
            Traffic_destroy(trafficSimulation);
            trafficSimulation = IntPtr.Zero;
        }
    }
}

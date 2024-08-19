using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class SimulationBridge : MonoBehaviour
{
    // This tells Unity to look for a library named "TrafficSimulation"
    const string DllName = "TrafficSimulation";

    [DllImport(DllName)]
    private static extern IntPtr Traffic_create(int num_agents, uint seed);

    [DllImport(DllName)]
    private static extern void Traffic_destroy(IntPtr traffic);

    [DllImport(DllName)]
    private static extern void Traffic_step(IntPtr traffic, float[] high_level_actions, float[] low_level_actions, int num_actions);

    // More DllImport declarations for other functions...

    private IntPtr trafficSimulation;

    void Start()
    {
        // Create the traffic simulation
        trafficSimulation = Traffic_create(10, 12345); // Create simulation with 2 agents and seed 12345
    }

    void Update()
    {
        // Example of stepping the simulation
        float[] highLevelActions = new float[10];  // Fill this with actual data
        float[] lowLevelActions = new float[10];   // Fill this with actual data
        Traffic_step(trafficSimulation, highLevelActions, lowLevelActions, 10);

        // ... use other functions to get simulation state ...
    }

    void OnDestroy()
    {
        // Clean up when the script is destroyed
        if (trafficSimulation != IntPtr.Zero)
        {
            Traffic_destroy(trafficSimulation);
            trafficSimulation = IntPtr.Zero;
        }
    }
}

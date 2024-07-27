using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;


public class client : MonoBehaviour
{
    private TcpClient socketConnection;
    private NetworkStream stream;
    //private StreamWriter writer;

    private const string serverAddress = "127.0.0.1"; //"192.168.178.88";
    private const int serverPort = 12350;
    private bool socketReady = false;

    public GameObject agentPrefab;  // Reference to the agent prefab (e.g., Mercedes-Benz AMG GT-R)
    private Dictionary<string, GameObject> agentInstances = new Dictionary<string, GameObject>(); // Dictionary to store agent instances // agents

    private void Start()
    {
        ConnectToServer();   // Connect to Python server
        SendCommand("step"); // Send a command to Python server
        StartCoroutine(ReceiveDataCoroutine());
    }

    /*
      If you have a large number of agents, updating their positions every frame
    in the Update method could potentially impact performance. In that case,
    you might want to consider updating the agent positions
    in the ReceiveDataCoroutine method instead, and only update the positions
    when new data is received from the server.
     */

    /*
    private void Update()
    {
        // Update agent positions based on the received render data
        foreach (var agentData in agentInstances.Values)
        {
            // Access the position data from the agentData object and update the transform
            // Example: agentData.transform.position = new Vector3(...);
        }
    }
    */

    private void ConnectToServer()
    {
        try
        {
            socketConnection = new TcpClient(serverAddress, serverPort);
            stream = socketConnection.GetStream();
            //writer = new StreamWriter(stream, Encoding.ASCII);


            socketReady = true;
            Debug.Log("Connected to server.");
        }
        catch (Exception e)
        {
            Debug.LogError($"Socket exception: {e}");
        }
    }

    void SendCommand(string command)
    {
        if (socketReady)
        {
            try
            {
                Debug.Log("Send command: " + command);
                byte[] sendData = Encoding.ASCII.GetBytes(command);
                stream.Write(sendData, 0, sendData.Length);
            }
            catch (SocketException se)
            {
                Debug.LogError("Socket exception: " + se.Message);
                // Handle the exception, possibly attempt reconnection or other recovery mechanisms
            }
        }
    }

    private IEnumerator ReceiveDataCoroutine()
    {
        Debug.Log("ReceiveDataCoroutine ...");

        byte[] receiveBuffer = new byte[1024]; // Increased buffer size from 1024

        while (true)
        {
            try
            {
                int bytesRead = stream.Read(receiveBuffer, 0, receiveBuffer.Length);

                if (bytesRead == 0)
                {
                    Debug.Log("Connection closed by server.");
                    break;
                }

                // Assuming 'jsonString' is your JSON data as a string
                string dataReceived = Encoding.ASCII.GetString(receiveBuffer, 0, bytesRead);
                Debug.Log($"Received render data: {dataReceived}");
                  
                // Create a list to store agent IDs present in the received render data
                RenderData renderData = JsonUtility.FromJson<RenderData>(dataReceived);

                // Create a list to store agent IDs present in the received render data
                List<string> presentAgentIds = new();

                // Update agent positions in Unity scene
                foreach (AgentInfo agentInfo in renderData.agents)
                {
                    Debug.Log($"Processing Agent ID: {agentInfo.agent_id}");
                    presentAgentIds.Add(agentInfo.agent_id);
                    InstantiateOrUpdateAgent(agentInfo.agent_id, agentInfo);

                }

                // Create a temporary list of keys
                List<string> agentIdsToRemove = new List<string>(agentInstances.Keys);

                // Iterate over the temporary list of keys
                foreach (string agentId in agentIdsToRemove)
                {
                    // Destroy agent instances that are no longer present in the received render data
                    if (!presentAgentIds.Contains(agentId))
                    {
                        Destroy(agentInstances[agentId]);
                        agentInstances.Remove(agentId);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error receiving data: {e}");
            }

            yield return null; // Allow Unity to handle other tasks
        }
    }

    //private void InstantiateOrUpdateAgent(int agentID, Vector3 position)
    private void InstantiateOrUpdateAgent(string agentID, AgentInfo agentInfo)
    {
        // Coordinate (x, z, y)
        Vector3 position = new Vector3(agentInfo.position[1], agentInfo.position[2], agentInfo.position[0]);

        // Convert Euler angles to Quaternion, Euler angles (roll, pitch, yaw)
        Quaternion rotation = Quaternion.Euler(agentInfo.orientation[0] * Mathf.Rad2Deg, agentInfo.orientation[2] * Mathf.Rad2Deg, agentInfo.orientation[1] * Mathf.Rad2Deg);

        // Get the main camera
        Camera mainCamera = Camera.main;

        // Convert world position to viewport position
        Vector3 viewportPoint = mainCamera.WorldToViewportPoint(position);

        // Check if the point is within the camera's view frustum
        bool isVisible = viewportPoint.x >= 0 && viewportPoint.x <= 1 &&
                         viewportPoint.y >= 0 && viewportPoint.y <= 1 &&
                         viewportPoint.z > 0;
        /*
        if (isVisible)
        {
            // The position is outside the camera's view frustum
            Debug.LogWarning($"Agent {agentID} position {position} is inside the camera's view frustum");
        }
        else
        {
            // The position is outside the camera's view frustum
            Debug.LogWarning($"Agent {agentID} position {position} is outside the camera's view frustum");

            // Clamp the viewport point to be within [0, 1] range for x and y
            viewportPoint.x = Mathf.Clamp(viewportPoint.x, 0.0f, 1.0f);
            viewportPoint.y = Mathf.Clamp(viewportPoint.y, 0.0f, 1.0f);

            // Convert the clamped viewport position back to a world position
            position = mainCamera.ViewportToWorldPoint(viewportPoint);
            Debug.Log($"Confined position in world space: {position}");
        }
        */

        // Create a new agent instance if it doesn't exist
        if (!agentInstances.ContainsKey(agentID))
        {
            Debug.Log("Instantiating agentPrefab: " + agentPrefab.name);

            // Instantiate the new GameObject with the specified position and orientation
            GameObject newAgent = Instantiate(agentPrefab, position, rotation); // Quaternion.identity

            newAgent.transform.SetParent(this.transform);  // Use 'this.transform' to refer to the TrafficSimulationManager's transform

            agentInstances.Add(agentID, newAgent);
        }
        else // Update the position of the agent instance based on the received data
        {
            agentInstances[agentID].transform.position = position;
            agentInstances[agentID].transform.rotation = rotation;
        }
    }

    void OnApplicationQuit()
    {
        // Close the network stream
        if (stream != null)
        {
            try
            {
                stream.Close();
                Debug.Log("Network stream closed.");
            }
            catch (Exception e)
            {
                Debug.LogError("Exception while closing network stream: " + e.Message);
            }
        }

        // Close the socket connection
        if (socketConnection != null)
        {
            try
            {
                if (socketConnection.Connected)
                {
                    socketConnection.Close();
                    Debug.Log("Socket connection closed.");
                }
                else
                {
                    Debug.Log("Socket connection already closed.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Exception while closing socket connection: " + e.Message);
            }
        }
    }
}

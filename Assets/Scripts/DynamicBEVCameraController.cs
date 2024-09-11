using UnityEngine;

public class DynamicBEVCameraController : MonoBehaviour
{
    public Transform targetObject; // The object the camera should follow
    public float cameraHeight = 150f; // The height of the camera above the target object
    public float cameraDistance = 5f; // The distance of the camera from the target object
    public float smoothSpeed = 5f; // The speed at which the camera smoothly follows the target
    public float cameraXPosition = 0f; // The fixed x-position of the camera

    private Transform cameraTransform;
    private Quaternion fixedRotation;
    private bool isSearching = true;

    void Start()
    {
        cameraTransform = Camera.main.transform;
        fixedRotation = Quaternion.Euler(90f, 0f, 90f); // Fixed rotation to look horizontally along the x-axis
    }

    void Update()
    {
        if (isSearching)
        {
            FindTarget();
        }
    }

    void FindTarget()
    {
        GameObject trafficManager = GameObject.Find("TrafficSimulationManager");
        if (trafficManager != null)
        {
            Transform agent = trafficManager.transform.Find("agent_0");
            if (agent != null)
            {
                targetObject = agent;
                isSearching = false;
                Debug.Log("Default target 'agent_0' set successfully.");
            }
        }
    }

    void LateUpdate()
    {
        if (targetObject != null)
        {
            // Calculate the desired camera position based on the target object's position
            Vector3 desiredPosition = new Vector3(cameraXPosition, targetObject.position.y + cameraHeight, targetObject.position.z - cameraDistance);

            // Smoothly interpolate the camera's position towards the desired position
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPosition, Time.deltaTime * smoothSpeed);

            // Set the camera's rotation to the fixed orientation
            cameraTransform.rotation = fixedRotation;
        }
    }

    public void SetTargetObject(Transform target)
    {
        targetObject = target;
        isSearching = false;
    }
}
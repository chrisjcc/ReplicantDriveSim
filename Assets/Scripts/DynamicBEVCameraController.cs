using UnityEngine;

/// <summary>
/// Controls a dynamic Bird's Eye View (BEV) camera that follows a target object in a Unity scene.
///
/// This script is responsible for:
/// 1. Automatically finding and following a default target ('agent_0') if not specified.
/// 2. Smoothly moving the camera to maintain a fixed height and distance from the target.
/// 3. Keeping the camera at a fixed X position and rotation while following the target.
///
/// Key Components:
/// - Target Following: Camera follows a specified or automatically found target object.
/// - Smooth Movement: Interpolates camera position for smooth following.
/// - Fixed Orientation: Maintains a fixed rotation for consistent view.
/// - Automatic Target Finding: Searches for a default target if none is specified.
///
/// Public Properties:
/// - targetObject: Transform of the object the camera should follow.
/// - cameraHeight: Float representing the height of the camera above the target (default 150f).
/// - cameraDistance: Float representing the distance of the camera behind the target (default 5f).
/// - smoothSpeed: Float controlling the smoothness of camera movement (default 5f).
/// - cameraXPosition: Float specifying the fixed X position of the camera (default 0f).
///
/// Key Methods:
/// - Start(): Initializes camera transform and fixed rotation.
/// - Update(): Continuously searches for target if not found.
/// - LateUpdate(): Updates camera position and rotation based on target's movement.
/// - FindTarget(): Attempts to find the default target 'agent_0'.
/// - SetTargetObject(Transform): Allows external setting of the target object.
///
/// Usage:
/// Attach this script to a Camera GameObject in the Unity scene. Set the target object in the
/// inspector or let the script find the default 'agent_0' target automatically.
///
/// Note:
/// - The camera maintains a fixed X position and rotation, suitable for side-scrolling or
///   forward-moving scenes.
/// - Adjust public properties in the Inspector to fine-tune camera behavior.
/// - The script assumes a specific scene structure with a 'TrafficSimulationManager' containing 'agent_0'.
/// - Consider adding error handling for cases where the target object is not found or becomes null.
/// - The fixed rotation (90f, 0f, 90f) orients the camera to look horizontally along the x-axis.
///
/// </summary>
public class DynamicBEVCameraController : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform targetObject; // The object the camera should follow
    public string targetManagerName = "TrafficSimulationManager"; // Name of the parent object containing targets
    public string defaultTargetName = "agent_0"; // Default target name to search for
    
    [Header("Camera Settings")]
    public Camera targetCamera; // The camera to control (if null, uses Camera.main)
    public float cameraHeight = 150f; // The height of the camera above the target object
    public float cameraDistance = 5f; // The distance of the camera from the target object
    public float smoothSpeed = 5f; // The speed at which the camera smoothly follows the target
    public float cameraXPosition = 0f; // The fixed x-position of the camera
    
    [Header("Search Settings")]
    public float searchInterval = 1f; // Time between target search attempts (in seconds)
    public int maxSearchAttempts = 10; // Maximum number of search attempts (-1 for unlimited)

    private Transform cameraTransform;
    private Quaternion fixedRotation;
    private bool isSearching = true;
    private float lastSearchTime;
    private int searchAttempts = 0;

    /// <summary>
    /// Initializes key components of the dynamic BEV camera controller when the script starts.
    ///
    /// This method is automatically called by Unity when the script instance is being loaded.
    /// It performs two main operations:
    /// 1. Retrieves and stores the main camera's transform for efficient access.
    /// 2. Sets up a fixed rotation for the camera to maintain a consistent view.
    ///
    /// Functionality:
    /// - Assigns Camera.main.transform to cameraTransform for optimized transform manipulation.
    /// - Creates a fixed Quaternion rotation (90, 0, 90 degrees) stored in fixedRotation.
    ///
    /// Key Aspects:
    /// - cameraTransform: Cached for performance, avoiding repeated Camera.main calls.
    /// - fixedRotation: Set to look horizontally along the x-axis, providing a side view.
    ///
    /// Usage:
    /// This method is called automatically and doesn't need manual invocation.
    ///
    /// Note:
    /// - Ensure that a Camera tagged as "MainCamera" exists in the scene.
    /// - The fixed rotation (90f, 0f, 90f) results in:
    ///   * 90-degree rotation around X-axis (camera looks downward)
    ///   * 90-degree rotation around Z-axis (aligns view horizontally)
    /// - If a different camera orientation is needed, adjust the Euler angles here.
    /// - Consider error handling if Camera.main is null (no main camera in scene).
    ///
    /// </summary>
    void Start()
    {
        // Use assigned camera or fall back to Camera.main
        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        
        if (camera == null)
        {
            Debug.LogError("DynamicBEVCameraController: No camera found! Please assign a camera or ensure Camera.main is available.");
            enabled = false; // Disable the script if no camera is available
            return;
        }
        
        cameraTransform = camera.transform;
        fixedRotation = Quaternion.Euler(90f, 0f, 90f); // Fixed rotation to look horizontally along the x-axis
        
        // If no target is assigned, start searching
        if (targetObject == null)
        {
            isSearching = true;
            lastSearchTime = Time.time;
        }
        else
        {
            isSearching = false;
        }
    }

    /// <summary>
    /// Continuously checks if a target object needs to be found and initiates the search if necessary.
    ///
    /// This method is automatically called by Unity every frame.
    /// Its primary function is to:
    /// 1. Check if the controller is in a searching state.
    /// 2. Initiate the target finding process if searching is required.
    ///
    /// Functionality:
    /// - Checks the isSearching flag to determine if a target search is needed.
    /// - Calls the FindTarget method when isSearching is true.
    ///
    /// Key Aspects:
    /// - isSearching: Boolean flag indicating whether the controller needs to find a target.
    /// - Utilizes Unity's Update loop for continuous checking.
    ///
    /// Performance Considerations:
    /// - The condition check runs every frame, but FindTarget is only called when necessary.
    /// - Consider using a coroutine or invoking FindTarget less frequently if performance is a concern.
    ///
    /// Usage:
    /// This method is called automatically by Unity and doesn't need manual invocation.
    ///
    /// Note:
    /// - The isSearching flag should be set to false once a target is found to stop unnecessary searching.
    /// - This approach allows for dynamic target acquisition, useful if the initial target is not available
    ///   at start or if the target can change during runtime.
    /// - Consider adding a timeout or max attempts for FindTarget to prevent infinite searching.
    /// - If the target is expected to change frequently, you might want to implement a more sophisticated
    ///   target management system.
    ///
    /// </summary>
    void Update()
    {
        // Only search if we need to and enough time has passed since last search
        if (isSearching && Time.time - lastSearchTime >= searchInterval)
        {
            FindTarget();
            lastSearchTime = Time.time;
        }
        
        // Check if target still exists - restart search if it was destroyed
        if (!isSearching && targetObject == null)
        {
            Debug.LogWarning("DynamicBEVCameraController: Target object was destroyed. Restarting search...");
            RestartTargetSearch();
        }
    }

    /// <summary>
    /// Attempts to find and set a default target object for the camera to follow.
    ///
    /// This method performs the following steps:
    /// 1. Searches for a GameObject named "TrafficSimulationManager" in the scene.
    /// 2. If found, it looks for a child object named "agent_0" within the TrafficSimulationManager.
    /// 3. If "agent_0" is found, it sets it as the target for the camera to follow.
    ///
    /// Functionality:
    /// - Uses GameObject.Find to locate the TrafficSimulationManager in the scene.
    /// - Uses Transform.Find to locate "agent_0" as a child of TrafficSimulationManager.
    /// - Sets targetObject to the found agent's transform if successful.
    /// - Sets isSearching to false to stop further searching once a target is found.
    /// - Logs a success message when the default target is set.
    ///
    /// Key Aspects:
    /// - Assumes a specific scene hierarchy with TrafficSimulationManager containing agent_0.
    /// - Designed to work with a predefined naming convention for objects.
    ///
    /// Performance Considerations:
    /// - GameObject.Find is relatively slow and should be used sparingly, ideally not every frame.
    /// - Consider caching the TrafficSimulationManager reference if this method is called frequently.
    ///
    /// Error Handling:
    /// - Silently fails if TrafficSimulationManager or agent_0 is not found.
    /// - Does not reset isSearching to true if the target is not found, potentially stopping further searches.
    ///
    /// Usage:
    /// This method is typically called from the Update method when isSearching is true.
    ///
    /// Note:
    /// - Add error logging for cases where TrafficSimulationManager or agent_0 is not found.
    /// - Consider implementing a more robust search mechanism or allowing custom target specification.
    /// - The current implementation only searches once successfully. Implement additional logic if
    ///   you need to handle cases where the target might become null after being initially set.
    /// - Think about adding a timeout or maximum number of search attempts to prevent infinite searching.
    ///
    /// </summary>
    void FindTarget()
    {
        // Check if we've exceeded max search attempts
        if (maxSearchAttempts > 0 && searchAttempts >= maxSearchAttempts)
        {
            Debug.LogWarning($"DynamicBEVCameraController: Exceeded maximum search attempts ({maxSearchAttempts}). Stopping search.");
            isSearching = false;
            return;
        }
        
        searchAttempts++;
        
        // Search for the target manager
        GameObject trafficManager = GameObject.Find(targetManagerName);
        if (trafficManager == null)
        {
            Debug.LogWarning($"DynamicBEVCameraController: Could not find '{targetManagerName}' in scene (Attempt {searchAttempts}).");
            return;
        }
        
        // Search for the target within the manager
        Transform agent = trafficManager.transform.Find(defaultTargetName);
        if (agent == null)
        {
            Debug.LogWarning($"DynamicBEVCameraController: Could not find '{defaultTargetName}' in '{targetManagerName}' (Attempt {searchAttempts}).");
            return;
        }
        
        // Target found successfully
        targetObject = agent;
        isSearching = false;
        searchAttempts = 0; // Reset for future searches
        Debug.Log($"DynamicBEVCameraController: Target '{defaultTargetName}' found and set successfully.");
    }

    /// <summary>
    /// Updates the camera's position and rotation to follow the target object smoothly.
    ///
    /// This method is automatically called by Unity after all Update functions have been called.
    /// It performs the following operations when a target object is available:
    /// 1. Calculates the desired camera position based on the target's position and camera settings.
    /// 2. Smoothly interpolates the camera's current position towards the desired position.
    /// 3. Sets the camera's rotation to maintain a fixed orientation.
    ///
    /// Functionality:
    /// - Computes desired position using:
    ///   * Fixed X position (cameraXPosition)
    ///   * Target's Y position plus cameraHeight
    ///   * Target's Z position minus cameraDistance
    /// - Uses Vector3.Lerp for smooth camera movement.
    /// - Applies fixedRotation to maintain consistent camera orientation.
    ///
    /// Key Aspects:
    /// - Runs in LateUpdate to ensure it uses the most up-to-date target position.
    /// - Smoothing factor: Time.deltaTime * smoothSpeed for frame-rate independent smoothing.
    /// - Maintains a fixed camera rotation regardless of target movement.
    ///
    /// Parameters used:
    /// - cameraXPosition: Fixed X-coordinate for the camera.
    /// - cameraHeight: Vertical offset from the target.
    /// - cameraDistance: Distance behind the target (in Z-axis).
    /// - smoothSpeed: Controls the speed of camera position interpolation.
    ///
    /// Performance Considerations:
    /// - Efficient use of Vector3.Lerp for smooth movement.
    /// - No unnecessary calculations if targetObject is null.
    ///
    /// Usage:
    /// This method is called automatically by Unity and doesn't need manual invocation.
    ///
    /// Note:
    /// - Ensure targetObject, cameraTransform, and fixedRotation are properly initialized.
    /// - The camera maintains a fixed X position, suitable for side-scrolling or forward-moving scenes.
    /// - Consider adding bounds checking to prevent the camera from moving into undesired areas.
    /// - For more complex camera behaviors, you might need to modify the position and rotation calculations.
    /// - The current implementation assumes the target object's Z-axis represents forward movement.
    ///
    /// </summary>
    void LateUpdate()
    {
        // Only update camera if we have both a camera transform and a target
        if (cameraTransform != null && targetObject != null)
        {
            // Calculate the desired camera position based on the target object's position
            Vector3 desiredPosition = new Vector3(cameraXPosition, targetObject.position.y + cameraHeight, targetObject.position.z - cameraDistance);

            // Smoothly interpolate the camera's position towards the desired position
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPosition, Time.deltaTime * smoothSpeed);

            // Set the camera's rotation to the fixed orientation
            cameraTransform.rotation = fixedRotation;
        }
    }

    /// <summary>
    /// Sets a new target object for the camera to follow and stops the automatic target search.
    ///
    /// This public method allows external scripts to manually set the target for the camera,
    /// overriding any automatically found target or initiating camera following if no target was previously set.
    ///
    /// Functionality:
    /// - Assigns the provided Transform to targetObject.
    /// - Sets isSearching to false to stop any ongoing automatic target search.
    ///
    /// Parameters:
    /// - target: Transform of the new object for the camera to follow.
    ///
    /// Usage:
    /// Call this method from other scripts when you need to change or set the camera's target, e.g.:
    /// cameraController.SetTargetObject(newTargetTransform);
    ///
    /// Key Aspects:
    /// - Provides a way to dynamically change the camera's focus during runtime.
    /// - Stops the automatic search process, assuming manual control of target setting.
    ///
    /// Error Handling:
    /// - Does not check if the provided target is null. Consider adding a null check if needed.
    ///
    /// Note:
    /// - After calling this method, the camera will immediately start following the new target
    ///   in the next LateUpdate call.
    /// - If you need to revert to automatic target finding, you'll need to manually set
    ///   isSearching to true and targetObject to null.
    /// - Consider adding validation or event triggers when the target changes.
    /// - For more complex scenarios, you might want to add additional logic, such as:
    ///   * Smooth transition between targets
    ///   * Ability to clear the target (set to null) and resume searching
    ///   * Event invocation to notify other systems of target change
    ///
    /// </summary>
    /// <param name="target">The Transform of the new target object for the camera to follow.</param>
    /// <summary>
    /// Sets a new target object for the camera to follow and stops the automatic target search.
    /// </summary>
    /// <param name="target">The Transform of the new target object. Can be null to clear the target.</param>
    public void SetTargetObject(Transform target)
    {
        targetObject = target;
        
        if (target != null)
        {
            isSearching = false;
            searchAttempts = 0;
            Debug.Log($"DynamicBEVCameraController: Target manually set to '{target.name}'.");
        }
        else
        {
            Debug.Log("DynamicBEVCameraController: Target cleared. Restarting search if enabled.");
            if (enabled) // Only restart search if the component is enabled
            {
                RestartTargetSearch();
            }
        }
    }
    
    /// <summary>
    /// Restarts the target search process. Useful when the current target is lost.
    /// </summary>
    public void RestartTargetSearch()
    {
        isSearching = true;
        searchAttempts = 0;
        lastSearchTime = Time.time;
        Debug.Log("DynamicBEVCameraController: Target search restarted.");
    }
    
    /// <summary>
    /// Stops the target search process.
    /// </summary>
    public void StopTargetSearch()
    {
        isSearching = false;
        Debug.Log("DynamicBEVCameraController: Target search stopped.");
    }
    
    /// <summary>
    /// Returns whether the camera is currently searching for a target.
    /// </summary>
    public bool IsSearchingForTarget()
    {
        return isSearching;
    }
    
    /// <summary>
    /// Returns whether the camera has a valid target.
    /// </summary>
    public bool HasValidTarget()
    {
        return targetObject != null;
    }
}
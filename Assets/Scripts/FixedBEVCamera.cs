using UnityEngine;

/// <summary>
/// Positions and orients a camera for a fixed Bird's Eye View (BEV) perspective in a Unity scene.
///
/// This script is responsible for:
/// 1. Setting a fixed position for the camera, typically high above the scene.
/// 2. Orienting the camera to look at a specific point, usually the center of the area of interest.
///
/// Key Components:
/// - Fixed Position: Defined by the public Vector3 fixedPosition, defaulting to (10, 300, 0).
/// - Look-at Point: Defined by the public Vector3 lookAtPoint, defaulting to (0, 0, 0).
///
/// Public Properties:
/// - fixedPosition: Vector3 representing the camera's fixed position in world space.
///   Default is set high above the scene with a slight offset on the x-axis.
/// - lookAtPoint: Vector3 representing the point in world space the camera should face.
///   Default is the world origin (0, 0, 0).
///
/// Functionality:
/// - On Start, positions the camera at fixedPosition.
/// - Orients the camera to look at lookAtPoint.
///
/// Usage:
/// Attach this script to a Camera GameObject in the Unity scene. Adjust the fixedPosition and
/// lookAtPoint variables in the Inspector to achieve the desired view.
///
/// Note:
/// - The default fixedPosition assumes a scene centered around the world origin. Adjust as needed.
/// - Consider adding runtime methods to adjust the camera position or look-at point if dynamic
///   camera movement is required.
/// - The z-value of fixedPosition can be adjusted to move the camera closer to or further from
///   the center of the viewed area (e.g., a road in a traffic simulation).
/// - For large scenes, you might need to adjust the camera's far clipping plane to ensure
///   all necessary elements are visible.
///
/// </summary>
public class FixedBEVCamera : MonoBehaviour
{
    public Vector3 fixedPosition = new Vector3(10, 300, 0);  // Adjust the z-value to move the camera closer to the center of the road
    public Vector3 lookAtPoint = Vector3.zero;  // The point the camera looks at


    /// <summary>
    /// Initializes the camera's position and orientation when the script starts.
    ///
    /// This method is automatically called by Unity when the script instance is being loaded.
    /// It performs two main operations:
    /// 1. Sets the camera's position to the specified fixedPosition.
    /// 2. Orients the camera to look at the specified lookAtPoint.
    ///
    /// Functionality:
    /// - Uses transform.position to set the camera's position in world space.
    /// - Uses transform.LookAt to rotate the camera towards the look-at point.
    ///
    /// Usage:
    /// This method doesn't need to be called manually. Unity will invoke it
    /// when the GameObject this script is attached to becomes active in the scene.
    ///
    /// Note:
    /// - Ensure that fixedPosition and lookAtPoint are set to appropriate values in the Inspector
    ///   or via script before this method is called.
    /// - If you need to adjust the camera view at runtime, consider creating additional public
    ///   methods to modify position and look-at point.
    /// - This setup assumes a static camera view. For dynamic movement, you might need to
    ///   move this logic to Update or create a separate method for repositioning.
    ///
    /// </summary>
    void Start()
    {
        transform.position = fixedPosition;
        transform.LookAt(lookAtPoint);
    }
}

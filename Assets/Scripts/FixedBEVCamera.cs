using UnityEngine;

public class FixedBEVCamera : MonoBehaviour
{
    public Vector3 fixedPosition = new Vector3(10, 300, 0);  // Adjust the z-value to move the camera closer to the center of the road
    public Vector3 lookAtPoint = Vector3.zero;  // The point the camera looks at

    void Start()
    {
        transform.position = fixedPosition;
        transform.LookAt(lookAtPoint);
    }
}

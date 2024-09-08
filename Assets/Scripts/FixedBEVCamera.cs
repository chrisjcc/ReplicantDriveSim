using UnityEngine;

public class FixedBEVCamera : MonoBehaviour
{
    public Vector3 fixedPosition = new Vector3(10, 250, 0);  // Adjust Y value for height
    public Vector3 lookAtPoint = Vector3.zero;  // The point the camera looks at

    void Start()
    {
        transform.position = fixedPosition;
        transform.LookAt(lookAtPoint);
    }

    void LateUpdate()
    {
        // Ensure the camera stays in the fixed position
        transform.position = fixedPosition;
        transform.LookAt(lookAtPoint);
    }
}

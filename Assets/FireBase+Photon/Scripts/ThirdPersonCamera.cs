using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    public Transform Target;
    public Vector3 Offset = new Vector3(0, 2, -4);
    public float MouseSensitivity = 3.0f;
    public float MinY = -25f;
    public float MaxY = 80f;

    private float yaw = 0f;
    private float pitch = 0f;
 

    void LateUpdate()
    {
        if (Target == null) return;
     

        yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * MouseSensitivity;
        pitch = Mathf.Clamp(pitch, MinY, MaxY);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 targetPosition = Target.position + Vector3.up * 1.5f;

        Vector3 desiredPosition = targetPosition + rotation * Offset;
        transform.position = desiredPosition;
        transform.LookAt(targetPosition);
    }
}

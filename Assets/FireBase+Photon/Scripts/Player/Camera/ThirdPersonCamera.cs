using UnityEngine;

public class ThirdPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    public Vector3 Offset = new Vector3(0, 2, -4);
    public float MouseSensitivity = 3.0f;
    public float MinY = -25f;
    public float MaxY = 80f;

    private float yaw = 0f;
    private float pitch = 0f;
 

    void LateUpdate()
    {
        if (target == null) return;
     

        yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * MouseSensitivity;
        pitch = Mathf.Clamp(pitch, MinY, MaxY);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        Vector3 targetPosition = target.position + Vector3.up * 1.5f;

        Vector3 desiredPosition = targetPosition + rotation * Offset;
        transform.position = desiredPosition;
        transform.LookAt(targetPosition);
    }

    /// <summary>Starts following the supplied local player.</summary>
    public void Bind(Transform newTarget)
    {
        target = newTarget;
    }

    /// <summary>Stops following the supplied player if it is still the active target.</summary>
    public void Unbind(Transform oldTarget)
    {
        if (target == oldTarget)
        {
            target = null;
        }
    }
}

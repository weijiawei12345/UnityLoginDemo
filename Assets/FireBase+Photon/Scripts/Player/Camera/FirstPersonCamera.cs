using UnityEngine;

public class FirstPersonCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    public float MouseSensitivity = 12f;

    private float verticalRotation;
    private float horizontalRotation;

    private void LateUpdate()
    {
        if (target == null) return;

        // Match target position exactly
        transform.position = target.position;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        verticalRotation -= mouseY * MouseSensitivity;
        verticalRotation = Mathf.Clamp(verticalRotation, -70f, 70f);
        horizontalRotation += mouseX * MouseSensitivity;

        transform.rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0f);
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

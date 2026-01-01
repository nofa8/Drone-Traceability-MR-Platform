using UnityEngine;

public class DroneVisualizer : MonoBehaviour
{

    [Header("Debug")]
    public Vector3 spinAxis = Vector3.forward;

    [Header("Settings")]
    public float hoverRange = 0.05f; // Floats up/down by 5cm max
    public float tiltStrength = 2.0f; // Multiplier for tilt angle

    [Header("Propellers (Optional)")]
    public Transform[] propellers; // Drag your prop objects here
    public float propSpeed = 1000f;

    private Vector3 initialPos;

    void Start()
    {
        initialPos = transform.localPosition;
    }

    // Call this from DroneTelemetryController
    public void UpdateVisuals(DroneTelemetryData data)
    {
        // 1. CLAMPED ALTITUDE (Visual Feedback)
        // If drone is high (>10m), it hovers at max height on the dock.
        // If landing (0m), it sits on the dock.
        float normalizedAlt = Mathf.Clamp01((float)data.alt / 20f); // Max visual height at 20m real altitude
        float targetY = Mathf.Lerp(0, hoverRange, normalizedAlt);
        
        // Smoothly move local Y
        Vector3 targetPos = new Vector3(initialPos.x, initialPos.y + targetY, initialPos.z);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * 5f);

        // 2. PHYSICS MIMICRY (Tilt)
        // Forward speed (VelZ) -> Pitch (Nose down)
        // Right speed (VelX)   -> Roll (Tilt right)
        float pitch = (float)data.velZ * tiltStrength;
        float roll = -(float)data.velX * tiltStrength; // Negative because banking right means tilting right
        float yaw = (float)data.hdg;

        Quaternion targetRot = Quaternion.Euler(pitch, yaw, roll);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * 5f);

        // 3. PROPS
        if (propellers != null && propellers.Length > 0)
        {
            foreach(var prop in propellers)
            {
                if (prop != null)
                {
                    // Spin around the axis defined in the Inspector
                    prop.Rotate(spinAxis * 1000f * Time.deltaTime);
                }
            }
        }
    }
}
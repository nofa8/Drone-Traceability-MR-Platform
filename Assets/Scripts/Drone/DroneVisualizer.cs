using UnityEngine;

public class DroneVisualizer : MonoBehaviour
{
    [Header("Debug")]
    public Vector3 spinAxis = Vector3.forward;

    [Header("Settings")]
    public float hoverRange = 0.05f; 
    public float tiltStrength = 2.0f; 

    [Header("Propellers (Optional)")]
    public Transform[] propellers; 
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
        // FIX: alt -> altitude
        float normalizedAlt = Mathf.Clamp01((float)data.altitude / 20f); 
        float targetY = Mathf.Lerp(0, hoverRange, normalizedAlt);
        
        Vector3 targetPos = new Vector3(initialPos.x, initialPos.y + targetY, initialPos.z);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * 5f);

        // 2. PHYSICS MIMICRY (Tilt)
        // FIX: velZ/velX -> velocityZ/velocityX
        float pitch = (float)data.velocityZ * tiltStrength;
        float roll = -(float)data.velocityX * tiltStrength; 
        // FIX: hdg -> heading
        float yaw = (float)data.heading;

        Quaternion targetRot = Quaternion.Euler(pitch, yaw, roll);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * 5f);

        // 3. PROPS
        if (propellers != null && propellers.Length > 0)
        {
            foreach(var prop in propellers)
            {
                if (prop != null)
                {
                    prop.Rotate(spinAxis * 1000f * Time.deltaTime);
                }
            }
        }
    }
}
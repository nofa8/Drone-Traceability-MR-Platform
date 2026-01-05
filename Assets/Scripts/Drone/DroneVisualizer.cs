using UnityEngine;

public class DroneVisualizer : MonoBehaviour
{
    [Header("Debug")]
    public Vector3 spinAxis = Vector3.forward;

    [Header("Settings")]
    public float hoverRange = 0.5f; // Increased for better visibility
    public float tiltStrength = 2.0f; 
    public float smoothSpeed = 5f;

    [Header("Propellers")]
    public Transform[] propellers; 
    public float propSpeed = 1000f;

    // State
    private Vector3 initialPos;
    private Quaternion initialRot;
    private bool isActive = false; // Tracks if we are receiving data

    void Start()
    {
        initialPos = transform.localPosition;
        initialRot = transform.localRotation;
    }

    void Update()
    {
        // If inactive (disconnected), smoothly return to landing pad
        if (!isActive)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, initialPos, Time.deltaTime * 2f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, initialRot, Time.deltaTime * 2f);
        }
        else
        {
            // Spin props only when active
            SpinPropellers();
        }
    }

    // Call this when Data arrives
    public void UpdateVisuals(DroneTelemetryData data)
    {
        isActive = true; // We are live!

        // 1. Position (Altitude)
        float normalizedAlt = Mathf.Clamp01((float)data.altitude / 20f); 
        float targetY = Mathf.Lerp(0, hoverRange, normalizedAlt);
        
        // Keep X/Z at initial, only change Y (local height)
        Vector3 targetPos = new Vector3(initialPos.x, initialPos.y + targetY, initialPos.z);
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPos, Time.deltaTime * smoothSpeed);

        // 2. Rotation (Tilt + Heading)
        float pitch = (float)data.velocityZ * tiltStrength;
        float roll = -(float)data.velocityX * tiltStrength; 
        float yaw = (float)data.heading;

        Quaternion targetRot = Quaternion.Euler(pitch, yaw, roll);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRot, Time.deltaTime * smoothSpeed);
    }

    // Call this when Slot is Cleared
    public void ResetToIdle()
    {
        isActive = false; // Triggers the Update() landing logic
    }

    private void SpinPropellers()
    {
        if (propellers != null)
        {
            foreach(var prop in propellers)
            {
                if (prop) prop.Rotate(spinAxis * propSpeed * Time.deltaTime);
            }
        }
    }
}
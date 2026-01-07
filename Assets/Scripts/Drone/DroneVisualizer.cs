using UnityEngine;

public class DroneVisualizer : MonoBehaviour
{
    [Header("Debug")]
    public Vector3 spinAxis = Vector3.forward;

    [Header("Settings")]
    public float hoverRange = 0.5f; 
    public float tiltStrength = 2.0f; 
    public float smoothSpeed = 5f;

    [Header("Propellers")]
    public Transform[] propellers; 
    public float propSpeed = 1000f;

    // State
    private Vector3 initialPos;
    private Quaternion initialRot;
    
    // Telemetry State
    private bool isConnected = false;
    private bool areMotorsOn = false;
    private bool isFlying = false;
    
    // Target Values
    private Vector3 targetPos;
    private Quaternion targetRot;

    void Start()
    {
        initialPos = transform.localPosition;
        initialRot = transform.localRotation;
        
        // Initialize targets to start position
        targetPos = initialPos;
        targetRot = initialRot;
    }

    void Update()
    {
        // 1. Handle Disconnection / Offline
        if (!isConnected)
        {
            // Smoothly return to landing pad and stop
            transform.localPosition = Vector3.Lerp(transform.localPosition, initialPos, Time.deltaTime * 2f);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, initialRot, Time.deltaTime * 2f);
            return; 
        }

        // 2. Handle Physics / Visuals based on State
        
        // A. PROPELLERS: Spin if motors are on (regardless of flying)
        if (areMotorsOn)
        {
            SpinPropellers();
        }

        // B. POSITION: Only change Altitude if actually flying
        Vector3 finalPos = initialPos; // Default to ground
        
        if (isFlying)
        {
            // If flying, use the calculated targetPos (derived from altitude in UpdateVisuals)
            finalPos = targetPos;
        }
        // Else: finalPos stays at initialPos (Ground), even if motors are spinning (Idle)

        // Apply Position Smoothing
        transform.localPosition = Vector3.Lerp(transform.localPosition, finalPos, Time.deltaTime * smoothSpeed);

        // C. ROTATION: Always apply rotation if motors are on (allows checking tilt on ground), 
        // or force flat if off.
        Quaternion finalRot = areMotorsOn ? targetRot : initialRot;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, finalRot, Time.deltaTime * smoothSpeed);
    }

    // Call this when Data arrives
    public void UpdateVisuals(DroneTelemetryData data)
    {
        isConnected = true; 
        areMotorsOn = data.motorsOn;
        isFlying = data.isFlying;

        // 1. Calculate Target Position (Visual Altitude)
        // We normalize altitude (e.g., 0-20m) to a small local offset (0-0.5m)
        float normalizedAlt = Mathf.Clamp01((float)data.altitude / 20f); 
        float heightOffset = Mathf.Lerp(0, hoverRange, normalizedAlt);
        
        // We calculate the target, but we only USE it in Update() if isFlying == true
        targetPos = new Vector3(initialPos.x, initialPos.y + heightOffset, initialPos.z);

        // 2. Calculate Target Rotation (Tilt + Heading)
        float pitch = (float)data.velocityZ * tiltStrength; // Tilt forward/back
        float roll = -(float)data.velocityX * tiltStrength; // Tilt left/right
        float yaw = (float)data.heading;

        targetRot = Quaternion.Euler(pitch, yaw, roll);
    }

    // Call this when Slot is Cleared
    public void ResetToIdle()
    {
        isConnected = false;
        areMotorsOn = false;
        isFlying = false;
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
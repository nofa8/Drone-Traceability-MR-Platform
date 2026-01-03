using System;
using UnityEngine;

public class MockDroneBackend : MonoBehaviour, IDroneDataSource
{
    [Header("Simulation Settings")]
    public string droneID = "SIM-001";
    public float updateRate = 0.1f; // 10Hz, same as real drone
    public float movementSpeed = 0.5f;
    public float circleRadius = 5.0f; // 5 meters

    // Events
    public event Action<DroneTelemetryData> OnTelemetryReceived;

    // Internal State
    private bool isRunning = false;
    private float timer;
    private float startTime;
    private DroneTelemetryData currentData;

    void Awake()
    {
        // Initialize with default empty data
        currentData = new DroneTelemetryData
        {
            droneId = droneID, // FIX: id -> droneId
            model = "DJI Mavic 3 (Sim)",
            batteryLevel = 100.0, // FIX: batLvl -> batteryLevel
            online = true,
            satCount = 12,
            isFlying = true,
            motorsOn = true // FIX: areMotorsOn -> motorsOn
        };
    }

    public void Connect()
    {
        isRunning = true;
        startTime = Time.time;
        Debug.Log("🟢 Mock Backend Started");
    }

    public void Disconnect()
    {
        isRunning = false;
        Debug.Log("Pk Mock Backend Stopped");
    }

    void Update()
    {
        if (!isRunning) return;

        timer += Time.deltaTime;
        if (timer >= updateRate)
        {
            SimulateFlight();
            
            // 🔥 Push data to anyone listening (UI, 3D Model)
            OnTelemetryReceived?.Invoke(currentData);
            
            timer = 0;
        }
    }

    private void SimulateFlight()
    {
        float time = Time.time - startTime;

        // 1. FLY IN A CIRCLE (Physics Simulation)
        float angle = time * movementSpeed;
        
        // Calculate fake velocity
        // FIX: velX/velZ -> velocityX/velocityZ
        currentData.velocityX = Math.Cos(angle) * movementSpeed * circleRadius;
        currentData.velocityZ = Math.Sin(angle) * movementSpeed * circleRadius; 
        currentData.velocityY = 0; // FIX: velY -> velocityY

        // Update Heading
        float headingRad = Mathf.Atan2((float)currentData.velocityX, (float)currentData.velocityZ);
        currentData.heading = headingRad * Mathf.Rad2Deg; // FIX: hdg -> heading

        // Altitude
        // FIX: alt -> altitude
        currentData.altitude = 10.0 + Mathf.Sin(time * 0.5f) * 2.0f;

        // 2. DRAIN BATTERY
        // FIX: batLvl -> batteryLevel
        currentData.batteryLevel -= 0.05f * Time.deltaTime; 
        if (currentData.batteryLevel < 0) currentData.batteryLevel = 100;

        // 3. UPDATE POSITION
        // FIX: lat/lng -> latitude/longitude
        currentData.latitude = 39.74362 + (Math.Sin(angle) * 0.0001); 
        currentData.longitude = -8.80705 + (Math.Cos(angle) * 0.0001);
    }
}
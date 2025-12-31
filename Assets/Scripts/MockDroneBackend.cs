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
            id = droneID,
            model = "DJI Mavic 3 (Sim)",
            batLvl = 100.0,
            online = true,
            satCount = 12,
            isFlying = true,
            areMotorsOn = true
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
        // We simulate latitude/longitude changes as local meter offsets for now
        // In the real app, you'd map GPS to Unity coords.
        float angle = time * movementSpeed;
        
        // Calculate fake velocity based on the circle movement
        // Derivative of sin/cos is cos/-sin
        currentData.velX = Math.Cos(angle) * movementSpeed * circleRadius;
        currentData.velZ = Math.Sin(angle) * movementSpeed * circleRadius; // Unity Z is "North"
        currentData.velY = 0; // Flat flight

        // Update Heading (Face forward)
        float headingRad = Mathf.Atan2((float)currentData.velX, (float)currentData.velZ);
        currentData.hdg = headingRad * Mathf.Rad2Deg;

        // Altitude: Hover gently between 10m and 12m
        currentData.alt = 10.0 + Mathf.Sin(time * 0.5f) * 2.0f;

        // 2. DRAIN BATTERY
        currentData.batLvl -= 0.05f * Time.deltaTime; // Drains slowly
        if (currentData.batLvl < 0) currentData.batLvl = 100; // Recharge loop

        // 3. UPDATE POSITION (For the "Map" or "GPS")
        // Note: These are fake GPS offsets
        currentData.lat = 39.74362 + (Math.Sin(angle) * 0.0001); 
        currentData.lng = -8.80705 + (Math.Cos(angle) * 0.0001);
    }
}
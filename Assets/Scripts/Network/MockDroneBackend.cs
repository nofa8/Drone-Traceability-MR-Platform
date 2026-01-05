using System;
using UnityEngine;

public class MockDroneBackend : MonoBehaviour, IDroneDataSource
{
    [Header("Simulation Settings")]
    public string droneID = "SIM-001";
    public float updateRate = 0.1f; 
    public float movementSpeed = 0.2f; // Slower speed (radians/sec)
    
    [Header("Flight Path")]
    // 300m radius = nice big circle on your 1km map
    public float circleRadiusMeters = 300.0f; 
    
    // Center of your Map (Leiria)
    public double centerLat = 39.74362;
    public double centerLon = -8.80705;

    // Events
    public event Action<DroneTelemetryData> OnTelemetryReceived;

    private bool isRunning = false;
    private float timer;
    private float startTime;
    private DroneTelemetryData currentData;

    // Approx degrees per meter (at 40 deg lat)
    private const double DegPerMeterLat = 1.0 / 111111.0; 
    private const double DegPerMeterLon = 1.0 / (111111.0 * 0.766); // cos(40)

    void Awake()
    {
        currentData = new DroneTelemetryData
        {
            droneId = droneID,
            model = "Sim-Test-Unit",
            batteryLevel = 100.0,
            online = true,
            isFlying = true,
            motorsOn = true
        };
    }

    public void Connect()
    {
        isRunning = true;
        startTime = Time.time;
        Debug.Log("🟢 Mock Backend Started (Map Mode)");
    }

    public void Disconnect()
    {
        isRunning = false;
    }

    void Update()
    {
        if (!isRunning) return;

        timer += Time.deltaTime;
        if (timer >= updateRate)
        {
            SimulateFlight();
            
            // 🛑 OLD: Only fired local event
            // OnTelemetryReceived?.Invoke(currentData);
            
            // ✅ NEW: Push data to the Global System (Map & Dashboard)
            DroneNetworkClient.SendMockTelemetry(currentData);
            
            timer = 0;
        }
    }

    private void SimulateFlight()
    {
        float time = Time.time - startTime;

        // 1. Calculate Orbit
        float angle = time * movementSpeed;

        // 2. Meters Offset (Circle)
        double offsetX = Math.Cos(angle) * circleRadiusMeters;
        double offsetZ = Math.Sin(angle) * circleRadiusMeters;

        // 3. Convert to GPS
        currentData.latitude = centerLat + (offsetZ * DegPerMeterLat);
        currentData.longitude = centerLon + (offsetX * DegPerMeterLon);

        // 4. Update Physics & Heading
        currentData.heading = (angle * Mathf.Rad2Deg + 90) % 360; // Face tangent
        currentData.altitude = 50.0; // Steady 50m height
        
        // (Optional) Fake Velocity for tilt
        currentData.velocityX = Math.Cos(angle + Math.PI/2) * 10;
        currentData.velocityZ = Math.Sin(angle + Math.PI/2) * 10;
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

// --- REST API MODELS (Long Names) ---
// Matches GET /api/drones response
[Serializable]
public class PagedSnapshotResult
{
    public List<DroneSnapshotModel> items;
}

[Serializable]
public class DroneSnapshotModel
{
    // Matches dTITAN.Backend.Data.Mongo.Documents properties (CamelCase JSON)
    public string droneId;       // Backend: DroneId
    public string model;         // Backend: Model
    public bool isConnected;     // Backend: IsConnected
    public double batteryLevel;  // Backend: BatteryLevel
    public bool isFlying;        // Backend: IsFlying
    public bool areMotorsOn;
    public bool areLightsOn;
    public double latitude;
    public double longitude;
    public double altitude;
}

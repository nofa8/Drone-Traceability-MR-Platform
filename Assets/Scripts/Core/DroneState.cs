using System;
using UnityEngine;

[Serializable]
public class DroneState
{
    public string droneId;
    
    // The raw data (Position, Battery, Speed, etc.)
    public DroneTelemetryData data; 

    // Meta-data that the Network Client doesn't send, but we need
    public bool isConnected;
    public DateTime lastHeartbeatTime;

    // Computed property for "Staleness"
    public bool IsStale => (DateTime.UtcNow - lastHeartbeatTime).TotalSeconds > 5.0f;

    public DroneState(string id)
    {
        this.droneId = id;
        this.data = new DroneTelemetryData { droneId = id }; // Empty default
        this.isConnected = false;
        this.lastHeartbeatTime = DateTime.MinValue;
    }
}
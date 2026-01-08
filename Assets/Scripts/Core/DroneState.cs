using System;
using System.Collections.Generic; // Required for List<>
using UnityEngine;

[Serializable]
public class DroneState
{
    public string droneId;
    
    public DroneTelemetryData data; 
    public bool isConnected;
    public DateTime lastHeartbeatTime;

    // 🔥 NEW: The History Tape
    // We store a list of all data points received
    public List<DroneTelemetryData> history = new List<DroneTelemetryData>();

    public bool IsStale => (DateTime.UtcNow - lastHeartbeatTime).TotalSeconds > 5.0f;

    public DroneState(string id)
    {
        this.droneId = id;
        this.data = new DroneTelemetryData { droneId = id }; 
        this.isConnected = false;
        this.lastHeartbeatTime = DateTime.MinValue;
    }
}
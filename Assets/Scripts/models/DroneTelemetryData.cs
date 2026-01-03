using System;

[Serializable]
public class DroneTelemetryData
{
    // Identity
    public string droneId;
    public string model;
    
    // Position
    public double latitude;
    public double longitude;
    public double altitude;
    
    // Physics
    public double velocityX;
    public double velocityY;
    public double velocityZ;
    public double heading;
    
    // Status
    public double batteryLevel; // Normalized to match Internal usage
    public double batteryTemp;
    public int satCount;
    
    // Flags
    public bool isFlying;
    public bool online;
    public bool motorsOn;
    public bool lightsOn;
}
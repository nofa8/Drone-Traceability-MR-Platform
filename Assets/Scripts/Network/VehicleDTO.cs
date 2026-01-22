using System;

/// <summary>
/// Wrapper for OtherAPP WebSocket messages.
/// Format: { "userId": "...", "role": "drone", "message": { ... } }
/// </summary>
[Serializable]
public class VehicleMessageWrapper
{
    public string userId;
    public string role;
    public VehicleDTO message;
}

/// <summary>
/// Raw telemetry data from OtherAPP backend.
/// This is inside the "message" field of the wrapper.
/// </summary>
[Serializable]
public class VehicleDTO
{
    // Identity
    public string id;       // Note: OtherAPP uses "id", not "droneId"
    public string model;
    
    // Home Location (nested)
    public VehicleHomeLocation homeLocation;
    
    // Position (Note: uses "alt" not "altitude")
    public double lat;
    public double lng;
    public double alt;
    public double heading;
    
    // Velocity
    public double velocityX;
    public double velocityY;
    public double velocityZ;
    
    // Status
    public double battery;
    public bool isFlying;
    public bool online;
    public bool areMotorsOn;
    public bool areLightsOn;
    
    // Autopilot State
    public bool isGoingHome;
    public bool isMissionActive;
    public int missionStep;
    
    // Gimbal (Optional)
    public double gimbalPitch;
    public double gimbalYaw;
    public double gimbalRoll;
    
    // Virtual Stick Inputs (-1.0 to 1.0)
    public double stickYaw;
    public double stickPitch;
    public double stickRoll;
    public double stickThrottle;
}

[Serializable]
public class VehicleHomeLocation
{
    public double lat;
    public double lng;
}

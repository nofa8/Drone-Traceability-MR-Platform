using System;

// 1. The Wrapper (Top Level)
[Serializable]
public class WS_TelemetryEvent
{
    public string eventType;  // Matches "eventType"
    public string timeStamp;  // Matches "timeStamp"
    public WS_TelemetryPayload payload; // Matches "payload"
}

[Serializable]
public class WS_DisconnectEvent
{
    public string eventType;
    public string timeStamp;
    public string payload;   // The DroneID string
}

// 2. The Payload Layer
[Serializable]
public class WS_TelemetryPayload
{
    public string droneId;
    public string model;
    public WS_TelemetryDetails telemetry;
}

// 3. The Details Layer
[Serializable]
public class WS_TelemetryDetails
{
    public string timestamp;
    public WS_HomeLocation homeLocation;

    public double latitude;
    public double longitude;
    public double altitude;

    public double velocityX;
    public double velocityY;
    public double velocityZ;

    public double batteryLevel;       
    public double batteryTemperature; 
    
    public double heading;
    public int satelliteCount;
    public double remainingFlightTime;

    public bool isTraveling;
    public bool isFlying;
    public bool online;
    public bool isGoingHome;
    public bool isHomeLocationSet;
    public bool areMotorsOn;
    public bool areLightsOn;
}

[Serializable]
public class WS_HomeLocation
{
    public double latitude;
    public double longitude;
}

[Serializable]
public class WS_EventProbe
{
    public string eventType;
}

// 1. The Wrapper (The Envelope)
[Serializable]
public class WS_CommandEnvelope
{
    public string eventType = "Command";
    public string droneId;
    // We cannot serialize "object" or abstract classes with JsonUtility.
    // So we will serialize the command separately and inject it, or use specific envelopes.
}

// 2. Flight Command Payload (Takeoff, Land, RTL)
[Serializable]
public class CMD_Flight
{
    public string _t = "FlightCommand"; // Discriminator
    public string Command; // "takeoff", "land", "startGoHome"
}

// 3. Utility Command Payload (Arm/Disarm)
[Serializable]
public class CMD_Utility
{
    public string _t = "UtilityCommand"; // Discriminator
    public string Command; // "motors"
    public bool State;     // true = ARM, false = DISARM
}
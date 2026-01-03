using System;

// 1. The Wrapper (Top Level)
[Serializable]
public class WS_TelemetryEvent
{
    public string EventType;
    public string TimeStamp; 
    public WS_TelemetryPayload Payload;
}

[Serializable]
public class WS_DisconnectEvent
{
    public string EventType;
    public string TimeStamp; 
    public string Payload;   
}

// 2. The Payload Layer
[Serializable]
public class WS_TelemetryPayload
{
    public string DroneId;
    public string Model;
    public WS_TelemetryDetails Telemetry;
}

// 3. The Details Layer (Must match JSON PascalCase exactly!)
[Serializable]
public class WS_TelemetryDetails
{
    public string Timestamp;
    public WS_HomeLocation HomeLocation;

    public double Latitude;
    public double Longitude;
    public double Altitude;

    public double VelocityX;
    public double VelocityY;
    public double VelocityZ;

    public double BatteryLevel;       
    public double BatteryTemperature; 
    
    public double Heading;
    public int SatelliteCount;
    public double RemainingFlightTime;

    public bool IsTraveling;
    public bool IsFlying;
    public bool Online;
    public bool IsGoingHome;
    public bool IsHomeLocationSet;
    public bool AreMotorsOn;
    public bool AreLightsOn;
}

[Serializable]
public class WS_HomeLocation
{
    public double Latitude;
    public double Longitude;
}

[Serializable]
public class WS_EventProbe
{
    public string EventType;
}
using System;

[Serializable]
public class DroneSnapshotModel
{
    // Must match JSON: "droneId"
    public string droneId;
    public string model;
    public bool isConnected;
    
    // The JSON has a nested "telemetry" object, so we need a matching class
    public DroneSnapshotTelemetry telemetry;
}

[Serializable]
public class DroneSnapshotTelemetry
{
    public string timestamp;

    public HomeLocation homeLocation;

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
public class HomeLocation
{
    public double latitude;
    public double longitude;
}


[Serializable]
public class PagedSnapshotResult
{
    public System.Collections.Generic.List<DroneSnapshotModel> items;
    public int totalCount;
}
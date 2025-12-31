using System;
using UnityEngine;

// This matches the JSON sent by your backend
[Serializable]
public class DroneTelemetryData
{
    public string id;
    public string model;
    
    // Position
    public double lat;
    public double lng;
    public double alt;
    
    // Physics
    public double velX;
    public double velY;
    public double velZ;
    public double hdg; // Heading
    
    // Status
    public double batLvl; // Matches [JsonPropertyName("batLvl")]
    public double batTemperature;
    public int satCount;
    public int rft; // Remaining Flight Time
    
    // Booleans
    public bool isFlying;
    public bool isTraveling;
    public bool online;
    public bool areMotorsOn;
}
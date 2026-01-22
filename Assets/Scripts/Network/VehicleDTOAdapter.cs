using System;

/// <summary>
/// Adapter Layer: Maps raw VehicleDTO (transport) to DroneTelemetryData (domain).
/// Updated to handle wrapper format: { userId, role, message: {...} }
/// </summary>
public static class VehicleDTOAdapter
{
    /// <summary>
    /// Converts wrapped WebSocket message to clean domain model.
    /// </summary>
    public static DroneTelemetryData FromWrapper(VehicleMessageWrapper wrapper)
    {
        if (wrapper == null || wrapper.message == null) return null;
        
        return ToTelemetry(wrapper.message, wrapper.userId);
    }
    
    /// <summary>
    /// Converts raw VehicleDTO to clean domain model.
    /// </summary>
    public static DroneTelemetryData ToTelemetry(VehicleDTO dto, string fallbackId = null)
    {
        if (dto == null) return null;
        
        // Use 'id' field, or fallback to wrapper's userId
        string droneId = !string.IsNullOrEmpty(dto.id) ? dto.id : fallbackId;
        
        if (string.IsNullOrEmpty(droneId))
        {
            return null; // Can't process without an ID
        }
        
        return new DroneTelemetryData
        {
            // Identity
            droneId = droneId,
            model = dto.model ?? "Unknown",
            
            // Position (Key mapping: lat→latitude, lng→longitude, alt→altitude)
            latitude = dto.lat,
            longitude = dto.lng,
            altitude = Math.Max(0, dto.alt), // Sanity: no negative altitude
            heading = NormalizeHeading(dto.heading),
            
            // Velocity
            velocityX = dto.velocityX,
            velocityY = dto.velocityY,
            velocityZ = dto.velocityZ,
            
            // Status
            batteryLevel = Math.Clamp(dto.battery, 0, 100),
            batteryTemp = 25.0, // Default - not in OtherAPP
            satCount = 12, // Default - not in OtherAPP
            
            // Flags
            isFlying = dto.isFlying,
            online = dto.online,
            motorsOn = dto.areMotorsOn,
            lightsOn = dto.areLightsOn,
            
            // Autopilot state
            isGoingHome = dto.isGoingHome,
            isMissionActive = dto.isMissionActive
        };
    }
    
    private static double NormalizeHeading(double heading)
    {
        heading = heading % 360;
        if (heading < 0) heading += 360;
        return heading;
    }
}

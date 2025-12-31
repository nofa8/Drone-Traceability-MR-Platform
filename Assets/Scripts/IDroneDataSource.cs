using System;

public interface IDroneDataSource
{
    // The UI subscribes to this event to get updates
    event Action<DroneTelemetryData> OnTelemetryReceived;

    // Start/Stop the data stream
    void Connect();
    void Disconnect();
}
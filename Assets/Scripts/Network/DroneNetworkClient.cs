using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DroneNetworkClient : MonoBehaviour
{
    [Header("Connection Settings")]
    [Tooltip("Use PC IP (e.g. 192.168.1.5) if on Quest")]
    public string serverUrl = "ws://192.168.1.64:8083"; 
    public string droneID = "RD001";
    public bool autoReconnect = true;

    private ClientWebSocket ws;
    private CancellationTokenSource cts;
    private bool isReconnecting = false;

    async void Start()
    {
        await Task.Delay(500); // Small startup delay
        await ConnectWithRetry();
    }

    async Task ConnectWithRetry()
    {
        int retryDelay = 1000; 
        const int maxDelay = 10000; 

        while (this != null && (ws == null || ws.State != WebSocketState.Open))
        {
            try
            {
                if (isReconnecting) Debug.Log($"🔄 Reconnecting in {retryDelay/1000}s...");
                
                ws = new ClientWebSocket();
                cts = new CancellationTokenSource();
                Uri uri = new Uri($"{serverUrl}");

                Debug.Log($"⏳ Connecting to {uri}...");
                await ws.ConnectAsync(uri, cts.Token);
                
                Debug.Log("✅ <color=green>Connected to Backend!</color>");
                isReconnecting = false;
                retryDelay = 1000; 

                await ReceiveLoop();
                
                if (!autoReconnect) break;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"⚠️ Connection Failed: {e.Message}");
            }

            if (this == null) break;
            await Task.Delay(retryDelay);
            retryDelay = Mathf.Min(retryDelay * 2, maxDelay);
            isReconnecting = true;
        }
    }

    async Task ReceiveLoop()
    {
        var buffer = new byte[8192];
        try
        {
            while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    MainThreadDispatcher.Enqueue(() => ProcessMessageSafe(json));
                }
            }
        }
        catch (Exception) { /* Expected disconnect/error, loop will catch it */ }
    }

    void ProcessMessageSafe(string json)
    {
        try
        {
            var probe = JsonUtility.FromJson<WS_EventProbe>(json);
            if (probe == null || string.IsNullOrEmpty(probe.EventType)) return;

            if (probe.EventType == "DroneTelemetryReceived")
            {
                ParseTelemetry(json);
            }
            else if (probe.EventType == "DroneDisconnected")
            {
                ParseDisconnect(json);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"💥 JSON Error: {e.Message}");
        }
    }

    void ParseTelemetry(string json)
    {
        var packet = JsonUtility.FromJson<WS_TelemetryEvent>(json);
        if (packet?.Payload?.Telemetry == null) return;

        // --- THE MAPPING (Raw -> Clean) ---
        DroneTelemetryData cleanData = new DroneTelemetryData();
        var p = packet.Payload;
        var t = p.Telemetry;

        cleanData.droneId = p.DroneId;
        cleanData.model = p.Model;
        
        cleanData.online = t.Online;
        cleanData.isFlying = t.IsFlying;
        cleanData.motorsOn = t.AreMotorsOn;
        cleanData.lightsOn = t.AreLightsOn;
        
        cleanData.latitude = t.Latitude;
        cleanData.longitude = t.Longitude;
        cleanData.altitude = t.Altitude;
        cleanData.heading = t.Heading;
        
        cleanData.velocityX = t.VelocityX;
        cleanData.velocityY = t.VelocityY;
        cleanData.velocityZ = t.VelocityZ;

        cleanData.batteryLevel = t.BatteryLevel;
        cleanData.batteryTemp = t.BatteryTemperature;
        cleanData.satCount = t.SatelliteCount;

        if (FleetUIManager.Instance != null)
            FleetUIManager.Instance.HandleLiveUpdate(cleanData);
    }

    void ParseDisconnect(string json)
    {
        var packet = JsonUtility.FromJson<WS_DisconnectEvent>(json);
        if (packet != null && !string.IsNullOrEmpty(packet.Payload))
        {
            DroneTelemetryData offlineData = new DroneTelemetryData();
            offlineData.droneId = packet.Payload;
            offlineData.online = false; 
            
            if (FleetUIManager.Instance != null)
                FleetUIManager.Instance.HandleLiveUpdate(offlineData);
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        ws?.Dispose();
    }
}
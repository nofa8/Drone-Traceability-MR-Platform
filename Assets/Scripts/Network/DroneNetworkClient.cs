using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DroneNetworkClient : MonoBehaviour
{
    // --- NEW EVENT ---
    // The Map listens to this to draw ALL drones at once
    public static event Action<DroneTelemetryData> OnGlobalTelemetry;

    [Header("Connection Settings")]
    [Tooltip("Use PC IP (e.g. 192.168.1.5) if on Quest")]
    public string serverUrl = "ws://192.168.1.64:5102"; 
    public string droneID = "RD001";
    public bool autoReconnect = true;

    private ClientWebSocket ws;
    private CancellationTokenSource cts;
    private bool isReconnecting = false;

    async void Start()
    {
        await Task.Delay(500); 
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

                string myClientID = "Dash-" + UnityEngine.Random.Range(1000, 9999);
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
        catch (Exception) { /* Expected disconnect */ }
    }

    void ProcessMessageSafe(string json)
    {
        try
        {
            var probe = JsonUtility.FromJson<WS_EventProbe>(json);
            if (probe == null || string.IsNullOrEmpty(probe.eventType)) return;

            if (probe.eventType == "DroneTelemetryReceived")
            {
                ParseTelemetry(json);
            }
            else if (probe.eventType == "DroneDisconnected")
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
        if (packet?.payload?.telemetry == null) return;

        DroneTelemetryData cleanData = new DroneTelemetryData();
        var p = packet.payload;
        var t = p.telemetry;

        cleanData.droneId = p.droneId;
        cleanData.model = p.model;
        
        cleanData.online = t.online;
        cleanData.isFlying = t.isFlying;
        cleanData.motorsOn = t.areMotorsOn;
        cleanData.lightsOn = t.areLightsOn;
        
        cleanData.latitude = t.latitude;
        cleanData.longitude = t.longitude;
        cleanData.altitude = t.altitude;
        cleanData.heading = t.heading;
        
        cleanData.velocityX = t.velocityX;
        cleanData.velocityY = t.velocityY;
        cleanData.velocityZ = t.velocityZ;

        cleanData.batteryLevel = t.batteryLevel;
        cleanData.batteryTemp = t.batteryTemperature;
        cleanData.satCount = t.satelliteCount;

        // 1. Send to Fleet Manager (UI List)
        if (FleetUIManager.Instance != null)
            FleetUIManager.Instance.HandleLiveUpdate(cleanData);

        // 2. Broadcast to Map (Global)
        OnGlobalTelemetry?.Invoke(cleanData);
    }

    void ParseDisconnect(string json)
    {
        var packet = JsonUtility.FromJson<WS_DisconnectEvent>(json);
        if (packet != null && !string.IsNullOrEmpty(packet.payload))
        {
            DroneTelemetryData offlineData = new DroneTelemetryData();
            offlineData.droneId = packet.payload;
            offlineData.online = false; 
            
            if (FleetUIManager.Instance != null)
                FleetUIManager.Instance.HandleLiveUpdate(offlineData);
            
            // Also notify map so it can maybe turn the dot gray?
             OnGlobalTelemetry?.Invoke(offlineData);
        }
    }

    private void OnDestroy()
    {
        cts?.Cancel();
        ws?.Dispose();
    }

    public static void SendMockTelemetry(DroneTelemetryData data)
    {
        // 1. Send to Map
        OnGlobalTelemetry?.Invoke(data);

        // 2. Send to Dashboard (in case it's not subscribed to Global yet)
        if (FleetUIManager.Instance != null)
            FleetUIManager.Instance.HandleLiveUpdate(data);
    }



    public async void SendCommand(string droneId, string commandType)
    {
        // 1. Create the Payload
        WS_CommandEvent cmd = new WS_CommandEvent
        {
            droneId = droneId,
            command = commandType
        };

        string json = JsonUtility.ToJson(cmd);

        // 2. Send via WebSocket (if connected)
        if (ws != null && ws.State == WebSocketState.Open)
        {
            try
            {
                ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
                await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, cts.Token);
                Debug.Log($"🚀 Sent Command: {commandType} to {droneId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Send Failed: {e.Message}");
            }
        }
        else
        {
            // Fallback for Debugging/Offline Mode
            Debug.Log($"⚠️ [Offline Mode] Pretending to send: {json}");
        }
    }

    public static void SendCommandGlobal(string commandType)
    {
        // Find the active drone
        if (SelectionManager.Instance == null) return;
        string droneId = SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId);

        if (string.IsNullOrEmpty(droneId)) 
        {
            Debug.LogWarning("❌ Cannot send command: No Drone Selected!");
            return;
        }

        // Find the client instance and send
        DroneNetworkClient client = FindObjectOfType<DroneNetworkClient>();
        if (client != null)
        {
            client.SendCommand(droneId, commandType);
        }
    }
}
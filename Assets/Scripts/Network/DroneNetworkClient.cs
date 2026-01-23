using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DroneNetworkClient : MonoBehaviour, INetworkReconfigurable
{
    // --- NEW EVENT ---
    // The Map listens to this to draw ALL drones at once
    public static event Action<DroneTelemetryData> OnGlobalTelemetry;

    [Header("Connection Settings")]
    [Tooltip("Drone ID to subscribe to")]
    public string droneID = "RD001";
    public bool autoReconnect = true;

    // INetworkReconfigurable
    public string ServiceName => "WebSocket Telemetry";

    // Runtime URL (loaded from NetworkConfig)
    private string ServerUrl => NetworkConfig.Instance.WebSocketUrl;

    private ClientWebSocket ws;
    private CancellationTokenSource cts;
    private bool isReconnecting = false;

    async void Start()
    {
        NetworkConfig.RegisterService(this);
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

                Debug.Log($"⏳ Connecting to {ServerUrl}?dboidsID={droneID}");
                Uri uri = new Uri($"{ServerUrl}?dboidsID={droneID}");

                // Debug.Log($"⏳ Connecting to {uri}...");
                await ws.ConnectAsync(uri, cts.Token);
                
                // Debug.Log("✅ <color=green>Connected to Backend!</color>");
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
        int messageCount = 0;
        
        // Debug.Log("📡 <color=cyan>ReceiveLoop started - waiting for messages...</color>");
        
        try
        {
            while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    messageCount++;
                    string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    // DEBUG: Log every 10th message to avoid spam
                    if (messageCount <= 3 || messageCount % 50 == 0)
                    {
                        Debug.Log($"📥 [Msg #{messageCount}] Received {result.Count} bytes");
                        // Show first 200 chars of JSON for debugging
                        string preview = json.Length > 200 ? json.Substring(0, 200) + "..." : json;
                        Debug.Log($"📦 JSON Preview: {preview}");
                    }
                    
                    MainThreadDispatcher.Enqueue(() => ProcessMessageSafe(json));
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.LogWarning($"🔌 Server sent Close: {result.CloseStatus} - {result.CloseStatusDescription}");
                }
            }
        }
        catch (Exception e) 
        { 
            Debug.LogWarning($"⚠️ ReceiveLoop ended: {e.Message}");
        }
        
        // Debug.Log($"📡 ReceiveLoop exited. Total messages: {messageCount}");
    }

    void ProcessMessageSafe(string json)
    {
        try
        {
            // --- OPTION 1: Try OtherAPP's wrapped format: { userId, role, message: {...} } ---
            var wrapper = JsonUtility.FromJson<VehicleMessageWrapper>(json);
            
            if (wrapper != null && wrapper.message != null && !string.IsNullOrEmpty(wrapper.userId))
            {
                // Debug.Log($"✅ <color=green>Parsed Wrapper: {wrapper.userId} role={wrapper.role} @ ({wrapper.message.lat:F4}, {wrapper.message.lng:F4}) alt={wrapper.message.alt:F1}m</color>");
                
                // Use Adapter Layer for clean mapping
                DroneTelemetryData cleanData = VehicleDTOAdapter.FromWrapper(wrapper);
                
                if (cleanData != null)
                {
                    // 1. Update Repository (Single Source of Truth)
                    if (DroneStateRepository.Instance != null)
                    {
                        DroneStateRepository.Instance.UpdateFromTelemetry(cleanData);
                    }
                    else
                    {
                        Debug.LogError("❌ DroneStateRepository.Instance is NULL!");
                    }

                    // 2. Broadcast to Map (Global)
                    OnGlobalTelemetry?.Invoke(cleanData);
                }
                return;
            }
            
            // --- OPTION 2: Fallback to legacy wrapped format (eventType) ---
            var probe = JsonUtility.FromJson<WS_EventProbe>(json);
            if (probe != null && !string.IsNullOrEmpty(probe.eventType))
            {
                Debug.Log($"📜 Legacy format detected: {probe.eventType}");
                
                if (probe.eventType == "DroneTelemetryReceived")
                {
                    ParseLegacyTelemetry(json);
                }
                else if (probe.eventType == "DroneDisconnected")
                {
                    ParseDisconnect(json);
                }
                return;
            }
            
            // --- Unknown format ---
            Debug.LogWarning($"⚠️ Unknown message format. First 100 chars: {json.Substring(0, Math.Min(100, json.Length))}");
        }
        catch (Exception e)
        {
            Debug.LogError($"💥 JSON Parse Error: {e.Message}\nJSON: {json.Substring(0, Math.Min(200, json.Length))}");
        }
    }

    // Legacy format support (can be removed once fully migrated)
    void ParseLegacyTelemetry(string json)
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

        if (DroneStateRepository.Instance != null)
            DroneStateRepository.Instance.UpdateFromTelemetry(cleanData);

        OnGlobalTelemetry?.Invoke(cleanData);
    }

    void ParseDisconnect(string json)
    {
        var packet = JsonUtility.FromJson<WS_DisconnectEvent>(json);
        if (packet != null && !string.IsNullOrEmpty(packet.payload))
        {
            if (DroneStateRepository.Instance != null)
                DroneStateRepository.Instance.MarkDisconnected(packet.payload);
            
            // Notify map to update visual state
            DroneTelemetryData offlineData = new DroneTelemetryData();
            offlineData.droneId = packet.payload;
            offlineData.online = false; 
            OnGlobalTelemetry?.Invoke(offlineData);
        }
    }

    private void OnDestroy()
    {
        NetworkConfig.UnregisterService(this);
        cts?.Cancel();
        ws?.Dispose();
    }

    /// <summary>
    /// INetworkReconfigurable: Called when user changes network settings.
    /// </summary>
    public void OnNetworkConfigChanged()
    {
        Debug.Log($"[WebSocket] Config changed, reconnecting to {ServerUrl}...");
        cts?.Cancel();
        ws?.Dispose();
        ws = null;
        isReconnecting = true;
        _ = ConnectWithRetry();
    }

    public static void SendMockTelemetry(DroneTelemetryData data)
    {
        // ✅ STEP 0: Feed Repository (Critical for Mock Data to appear in Detail View)
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.UpdateFromTelemetry(data);
        }

        // 1. Send to Map
        OnGlobalTelemetry?.Invoke(data);

        // 2. Send to Dashboard (Legacy/Backup)
        if (FleetUIManager.Instance != null)
            FleetUIManager.Instance.HandleLiveUpdate(data);
    }


    // --- TYPED COMMAND SENDERS ---

    public async void SendFlightCommand(string droneId, string action)
    {
        // Simulator: sendCommand("FlightCommand", { command: "takeoff" });
        // JSON: {"userId":"RD001", "role":"FlightCommand", "message":{"command":"takeoff"}}

        string json = $"{{\"userId\":\"{droneId}\",\"role\":\"FlightCommand\",\"message\":{{\"command\":\"{action}\"}}}}";
        
        await SendRaw(json, droneId, $"FlightCmd: {action}");
    }

    public async void SendUtilityCommand(string droneId, string action, bool state)
    {
        // Simulator: sendCommand("UtilityCommand", { command: "motors", state: true });
        // JSON: {"userId":"RD001", "role":"UtilityCommand", "message":{"command":"motors", "state":true}}

        string stateStr = state ? "true" : "false"; // JSON boolean is lowercase
        string json = $"{{\"userId\":\"{droneId}\",\"role\":\"UtilityCommand\",\"message\":{{\"command\":\"{action}\",\"state\":{stateStr}}}}}";

        await SendRaw(json, droneId, $"UtilityCmd: {action}={(state ? "ON" : "OFF")}");
    }

    // Helper (No changes needed here, just for context)
    private async Task SendRaw(string json, string droneId, string debugTag)
    {
        if (ws != null && ws.State == WebSocketState.Open)
        {
            try
            {
                ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
                await ws.SendAsync(bytesToSend, WebSocketMessageType.Text, true, cts.Token);
                // Debug.Log($"🚀 Sent {debugTag} to {droneId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Send Failed: {e.Message}");
            }
        }
        else
        {
            Debug.Log($"⚠️ [Offline] Pretending to send: {json}");
        }
    }

}
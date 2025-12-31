using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class DroneNetworkClient : MonoBehaviour
{
    // IMPORTANT: Use your PC's IP address (e.g., 192.168.1.5), NOT "localhost" 
    // because the Quest is a separate device on the network.
    [Tooltip("e.g. ws://192.168.1.64:8083")]
    public string serverUrl = "ws://192.168.1.64:8083"; 
    
    public string droneID = "RD001"; // Must match a simulated drone ID

    private ClientWebSocket ws;
    private CancellationTokenSource cts;

    // Event to update the UI
    public event Action<DroneTelemetryData> OnTelemetryReceived;

    async void Start()
    {
        await Connect();
    }

    async Task Connect()
    {
        ws = new ClientWebSocket();
        cts = new CancellationTokenSource();

        // The backend requires dboidsID and role in the URL or connection logic
        // Based on app.js, we connect as a "monitor" or "platform" to receive data
        Uri uri = new Uri($"{serverUrl}?dboidsID={droneID}&role=platform");

        try
        {
            await ws.ConnectAsync(uri, cts.Token);
            Debug.Log("✅ Connected to Drone Backend!");
            
            // Start listening loop
            _ = ReceiveLoop(); 
        }
        catch (Exception e)
        {
            Debug.LogError($"WebSocket Error: {e.Message}");
        }
    }

    async Task ReceiveLoop()
    {
        var buffer = new byte[8192];
        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Parse on the main thread to be safe with UI updates
                MainThreadDispatcher.Enqueue(() => {
                    ProcessMessage(json);
                });
            }
        }
    }

    void ProcessMessage(string json)
    {
        // Based on app.js, the broker sends the raw message object
        try 
        {
            DroneTelemetryData data = JsonUtility.FromJson<DroneTelemetryData>(json);
            OnTelemetryReceived?.Invoke(data);
        }
        catch (Exception e) 
        {
            // Handle parsing errors (sometimes handshake messages aren't telemetry)
        }
    }

    private void OnDestroy()
    {
        if (ws != null) ws.Dispose();
        if (cts != null) cts.Cancel();
    }
}
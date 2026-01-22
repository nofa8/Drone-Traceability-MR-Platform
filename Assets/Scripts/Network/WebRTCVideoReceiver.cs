using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Text;
using UnityEngine.Networking;

#if UNITY_WEBRTC_INSTALLED
using Unity.WebRTC;
#endif

/// <summary>
/// WebRTC Receiver - Simplified WHEP client based on MediaMTX official example.
/// Removes trickle ICE complexity in favor of simpler, more reliable connection.
/// </summary>
public class WebRTCVideoReceiver : MonoBehaviour
{
    public static WebRTCVideoReceiver Instance;

    [Header("Server Configuration")]
    public string serverIP = "192.168.1.100";
    public int webrtcPort = 8889;
    public string whepPathFormat = "{0}/whep";

    [Header("Connection Settings")]
    public float connectionTimeout = 15f;

    [Header("Output")]
    public RawImage targetDisplay;

    [Header("Debug")]
    public bool verboseLogging = true;

    // Events
    public event Action<Texture> OnVideoReceived;
    public event Action<ConnectionState> OnConnectionStateChanged;

    public enum ConnectionState { Disconnected, Connecting, Connected, Failed }
    public ConnectionState CurrentState { get; private set; } = ConnectionState.Disconnected;

    private Coroutine connectionCoroutine;
    private Coroutine webrtcUpdateCoroutine;
    private string currentDroneId;
    private int frameCount;

#if UNITY_WEBRTC_INSTALLED
    private RTCPeerConnection peerConnection;
    private MediaStream receiveStream;
#endif

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void OnDestroy()
    {
        Stop();
    }

    public void Play(string droneId)
    {
        if (string.IsNullOrEmpty(droneId)) return;

        Stop();
        currentDroneId = droneId;
        connectionCoroutine = StartCoroutine(ConnectWHEP());
    }

    public void Stop()
    {
        if (connectionCoroutine != null)
        {
            StopCoroutine(connectionCoroutine);
            connectionCoroutine = null;
        }

        if (webrtcUpdateCoroutine != null)
        {
            StopCoroutine(webrtcUpdateCoroutine);
            webrtcUpdateCoroutine = null;
        }

#if UNITY_WEBRTC_INSTALLED
        CleanupPeerConnection();
#endif
        SetState(ConnectionState.Disconnected);
    }

#if UNITY_WEBRTC_INSTALLED
    IEnumerator ConnectWHEP()
    {
        SetState(ConnectionState.Connecting);
        frameCount = 0;

        string endpoint = $"http://{serverIP}:{webrtcPort}/{string.Format(whepPathFormat, currentDroneId)}";
        Log($"🔌 Connecting to: {endpoint}");

        // Create peer connection (no ICE servers needed for local/simple setups)
        peerConnection = new RTCPeerConnection();
        receiveStream = new MediaStream();

        // Handle incoming tracks via MediaStream (official pattern)
        peerConnection.OnTrack = e =>
        {
            Log($"📺 OnTrack: {e.Track.Kind}");
            receiveStream.AddTrack(e.Track);
        };

        receiveStream.OnAddTrack = e =>
        {
            if (e.Track is VideoStreamTrack videoTrack)
            {
                Log("🎬 Video track added");
                videoTrack.OnVideoReceived += OnVideoFrame;
            }
        };

        // Monitor ICE connection state
        peerConnection.OnIceConnectionChange = state =>
        {
            Log($"🧊 ICE: {state}");
            if (state == RTCIceConnectionState.Connected)
                SetState(ConnectionState.Connected);
            else if (state == RTCIceConnectionState.Failed)
                Fail("ICE connection failed");
        };

        // Add video transceiver (receive only)
        var init = new RTCRtpTransceiverInit { direction = RTCRtpTransceiverDirection.RecvOnly };
        peerConnection.AddTransceiver(TrackKind.Video, init);

        // Start WebRTC update loop (official recommendation)
        webrtcUpdateCoroutine = StartCoroutine(WebRTC.Update());

        // Create and set local offer
        var offerOp = peerConnection.CreateOffer();
        yield return offerOp;
        if (offerOp.IsError) { Fail("CreateOffer failed"); yield break; }

        var offer = offerOp.Desc;
        var setLocalOp = peerConnection.SetLocalDescription(ref offer);
        yield return setLocalOp;
        if (setLocalOp.IsError) { Fail("SetLocalDescription failed"); yield break; }

        // POST offer to WHEP endpoint
        using var req = new UnityWebRequest(endpoint, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(offer.sdp));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/sdp");
        req.timeout = 10;

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Fail($"WHEP POST failed: {req.error}");
            yield break;
        }

        Log("✅ WHEP response received");

        // Set remote answer
        var answer = new RTCSessionDescription
        {
            type = RTCSdpType.Answer,
            sdp = req.downloadHandler.text
        };

        var setRemoteOp = peerConnection.SetRemoteDescription(ref answer);
        yield return setRemoteOp;
        if (setRemoteOp.IsError) { Fail("SetRemoteDescription failed"); yield break; }

        Log("✅ Remote description set, waiting for ICE...");

        // Wait for connection with timeout
        float elapsed = 0f;
        while (CurrentState == ConnectionState.Connecting && elapsed < connectionTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (CurrentState != ConnectionState.Connected)
            Fail($"Connection timeout after {elapsed:F1}s");
    }

    void OnVideoFrame(Texture tex)
    {
        if (frameCount == 0)
            Log($"🖼️ FIRST FRAME! ({tex.width}x{tex.height})");

        frameCount++;
        if (targetDisplay) targetDisplay.texture = tex;
        OnVideoReceived?.Invoke(tex);
    }

    void CleanupPeerConnection()
    {
        receiveStream?.Dispose();
        receiveStream = null;

        peerConnection?.Close();
        peerConnection?.Dispose();
        peerConnection = null;
    }
#endif

    void Fail(string reason)
    {
        Debug.LogError($"[WebRTC] ❌ {reason}");
        SetState(ConnectionState.Failed);
    }

    void SetState(ConnectionState s)
    {
        if (CurrentState == s) return;
        Log($"📊 State: {CurrentState} → {s}");
        CurrentState = s;
        OnConnectionStateChanged?.Invoke(s);
    }

    void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"[WebRTC] {msg}");
    }
}

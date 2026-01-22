using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Video Stream Controller - WebRTC Only (HLS removed).
/// Manages WebRTC video streaming for drone POV.
/// </summary>
public class VideoStreamController : MonoBehaviour
{
    public static VideoStreamController Instance;

    [Header("Receiver")]
    public WebRTCVideoReceiver webrtcReceiver;

    [Header("Settings")]
    public float connectionTimeout = 15f;
    
    [Header("Debug")]
    public bool verboseLogging = true;

    // Events
    public event Action<Texture> OnVideoReceived;
    public event Action<StreamStatus> OnStatusChanged;

    public enum StreamStatus { Offline, Connecting, Live, Failed }
    public StreamStatus CurrentStatus { get; private set; } = StreamStatus.Offline;
    public Texture CurrentTexture { get; private set; }

    private string currentDroneId;
    private Coroutine streamCoroutine;
    private bool isSubscribed = false;
    private bool firstFrameReceived = false;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
        
        LogDebug("🎮 VideoStreamController.Awake()");
    }

    IEnumerator Start()
    {
        LogDebug("🎮 VideoStreamController.Start() - Waiting for SelectionManager...");
        
        // Wait for SelectionManager
        float waited = 0f;
        while (SelectionManager.Instance == null && waited < 5f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (SelectionManager.Instance == null)
        {
            Debug.LogError("❌ VideoStreamController: SelectionManager not found!");
            yield break;
        }
        
        LogDebug("✅ SelectionManager found");

        // Verify receiver
        if (webrtcReceiver == null)
        {
            Debug.LogError("❌ VideoStreamController: webrtcReceiver is NULL! Assign in Inspector.");
            yield break;
        }
        LogDebug($"✅ WebRTC Receiver found");

        SelectionManager.Instance.OnActiveSlotChanged += OnSlotChanged;
        SelectionManager.Instance.OnSlotSelectionChanged += OnDroneAssigned;

        // Initial check
        int activeSlot = SelectionManager.Instance.ActiveSlotId;
        LogDebug($"🎮 Initial slot: {activeSlot}");
        OnSlotChanged(activeSlot);
    }

    void OnDestroy()
    {
        LogDebug("🎮 VideoStreamController.OnDestroy()");
        UnsubscribeFromReceiver();
        
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnActiveSlotChanged -= OnSlotChanged;
            SelectionManager.Instance.OnSlotSelectionChanged -= OnDroneAssigned;
        }
    }
    
    void LogDebug(string msg)
    {
        if (verboseLogging)
            Debug.LogWarning($"[VideoCtrl] {msg}");
    }

    // --- EVENT HANDLERS ---

    void OnSlotChanged(int slotId)
    {
        string droneId = SelectionManager.Instance?.GetDroneAtSlot(slotId);
        LogDebug($"🔄 Slot changed: {slotId} → drone='{droneId}'");
        
        if (!string.IsNullOrEmpty(droneId))
            Play(droneId);
        else
            Stop();
    }

    void OnDroneAssigned(int slotId, string droneId)
    {
        LogDebug($"🔄 Drone assigned: slot={slotId}, drone='{droneId}'");
        
        if (SelectionManager.Instance != null && slotId == SelectionManager.Instance.ActiveSlotId)
            OnSlotChanged(slotId);
    }

    // --- PUBLIC API ---

    public void Play(string droneId)
    {
        if (string.IsNullOrEmpty(droneId))
        {
            LogDebug("▶️ Play() with empty droneId - stopping");
            Stop();
            return;
        }

        LogDebug($"▶️ Play({droneId})");
        currentDroneId = droneId;
        firstFrameReceived = false;

        if (streamCoroutine != null)
        {
            StopCoroutine(streamCoroutine);
            LogDebug("   → Stopped previous coroutine");
        }

        streamCoroutine = StartCoroutine(StartWebRTCStream(droneId));
    }

    public void Stop()
    {
        LogDebug("⏹️ Stop()");

        if (streamCoroutine != null)
        {
            StopCoroutine(streamCoroutine);
            streamCoroutine = null;
        }

        UnsubscribeFromReceiver();
        webrtcReceiver?.Stop();

        CurrentTexture = null;
        SetStatus(StreamStatus.Offline);
    }

    // --- STREAM LOGIC ---

    IEnumerator StartWebRTCStream(string droneId)
    {
        SetStatus(StreamStatus.Connecting);
        LogDebug($"🔌 Starting WebRTC for {droneId}...");

        if (webrtcReceiver == null)
        {
            Debug.LogError("❌ webrtcReceiver is NULL!");
            SetStatus(StreamStatus.Failed);
            yield break;
        }

        // Subscribe to video events
        SubscribeToReceiver();
        
        // Start connection
        webrtcReceiver.Play(droneId);
        LogDebug($"📡 WebRTC.Play() called, waiting for connection...");

        // Wait for connection with detailed logging
        float elapsed = 0f;
        float logInterval = 2f;
        float nextLog = logInterval;
        
        while (elapsed < connectionTimeout)
        {
            var state = webrtcReceiver.CurrentState;
            
            // Log progress every 2 seconds
            if (elapsed >= nextLog)
            {
                LogDebug($"⏳ Waiting... {elapsed:F1}s, State: {state}");
                nextLog += logInterval;
            }
            
            if (state == WebRTCVideoReceiver.ConnectionState.Connected)
            {
                // ICE connected - wait for first frame before declaring Live
                LogDebug("🎉 WebRTC ICE CONNECTED! Waiting for first video frame...");
                yield break;
            }
            
            if (state == WebRTCVideoReceiver.ConnectionState.Failed)
            {
                LogDebug("❌ WebRTC reported FAILED state");
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        LogDebug($"❌ Connection failed (elapsed: {elapsed:F1}s, final state: {webrtcReceiver.CurrentState})");
        SetStatus(StreamStatus.Failed);
    }

    void SubscribeToReceiver()
    {
        if (!isSubscribed && webrtcReceiver != null)
        {
            webrtcReceiver.OnVideoReceived += HandleVideoFrame;
            webrtcReceiver.OnConnectionStateChanged += HandleStateChange;
            isSubscribed = true;
            LogDebug("📺 Subscribed to WebRTC events");
        }
    }

    void UnsubscribeFromReceiver()
    {
        if (isSubscribed && webrtcReceiver != null)
        {
            webrtcReceiver.OnVideoReceived -= HandleVideoFrame;
            webrtcReceiver.OnConnectionStateChanged -= HandleStateChange;
            isSubscribed = false;
            LogDebug("📺 Unsubscribed from WebRTC events");
        }
    }

    void HandleVideoFrame(Texture texture)
    {
        if (!firstFrameReceived)
        {
            firstFrameReceived = true;
            SetStatus(StreamStatus.Live);
            LogDebug("🎥 First video frame received — stream LIVE");
        }

        CurrentTexture = texture;
        OnVideoReceived?.Invoke(texture);
    }

    void HandleStateChange(WebRTCVideoReceiver.ConnectionState state)
    {
        LogDebug($"🔗 WebRTC state changed: {state}");
        
        // Don't set Live on Connected - wait for first frame
        if (state == WebRTCVideoReceiver.ConnectionState.Failed)
            SetStatus(StreamStatus.Failed);
    }

    void SetStatus(StreamStatus status)
    {
        if (CurrentStatus == status) return;
        
        StreamStatus oldStatus = CurrentStatus;
        CurrentStatus = status;
        LogDebug($"📊 Status: {oldStatus} → {status}");
        OnStatusChanged?.Invoke(status);
    }
}

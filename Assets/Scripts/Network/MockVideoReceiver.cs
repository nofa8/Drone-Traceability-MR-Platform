using UnityEngine;
using UnityEngine.Video;
using System;
using System.Collections;

[RequireComponent(typeof(VideoPlayer))]
public class MockVideoReceiver : MonoBehaviour
{
    public static MockVideoReceiver Instance;

    [Header("Network Simulation")]
    public float connectionLatency = 0.5f; 

    [Header("Video Source")]
    public VideoClip mockVideoFile; 

    // Data
    public Texture CurrentFeed { get; private set; }
    public event Action<Texture> OnStreamUpdated;

    private VideoPlayer videoPlayer;
    private string currentDroneId;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.playOnAwake = false;
        videoPlayer.isLooping = true;
        videoPlayer.renderMode = VideoRenderMode.APIOnly; 
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
    }

    IEnumerator Start()
    {
        // 1. Wait until SelectionManager is ready
        while (SelectionManager.Instance == null)
        {
            yield return null; 
        }

        // 2. Subscribe to BOTH events
        // Event A: User switches slots (e.g. Slot 1 -> Slot 2)
        SelectionManager.Instance.OnActiveSlotChanged += OnSlotChanged;
        
        // Event B: User assigns a drone (e.g. Empty -> Drone A) [THIS WAS MISSING]
        SelectionManager.Instance.OnSlotSelectionChanged += OnDroneAssigned;

        // 3. Trigger initial check
        OnSlotChanged(SelectionManager.Instance.ActiveSlotId);
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnActiveSlotChanged -= OnSlotChanged;
            SelectionManager.Instance.OnSlotSelectionChanged -= OnDroneAssigned;
        }
    }

    // 🔥 NEW HANDLER: Filters assignment events
    void OnDroneAssigned(int slotId, string droneId)
    {
        // Only refresh if the assignment happened on the slot we are currently watching
        if (SelectionManager.Instance != null && slotId == SelectionManager.Instance.ActiveSlotId)
        {
            OnSlotChanged(slotId);
        }
    }

    void OnSlotChanged(int slotId)
    {
        if (SelectionManager.Instance == null) return;

        string newDroneId = SelectionManager.Instance.GetDroneAtSlot(slotId);

        if (!string.IsNullOrEmpty(newDroneId))
        {
            currentDroneId = newDroneId;
            StopAllCoroutines(); // Stop any pending connections
            StartCoroutine(SimulateConnection());
        }
        else
        {
            Disconnect();
        }
    }

    IEnumerator SimulateConnection()
    {
        // Simulate Network Delay
        yield return new WaitForSeconds(connectionLatency);

        if (mockVideoFile != null)
        {
            videoPlayer.clip = mockVideoFile;
            videoPlayer.Prepare();

            // Wait for video to be ready
            while (!videoPlayer.isPrepared) yield return null;

            videoPlayer.Play();
            yield return null; 

            CurrentFeed = videoPlayer.texture;
            
            // Notify UI
            OnStreamUpdated?.Invoke(CurrentFeed);
        }
    }

    void Disconnect()
    {
        videoPlayer.Stop();
        CurrentFeed = null;
        OnStreamUpdated?.Invoke(null);
    }
}
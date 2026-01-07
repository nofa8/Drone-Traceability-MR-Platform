using UnityEngine;
using UnityEngine.Video;
using System;
using System.Collections;

[RequireComponent(typeof(VideoPlayer))]
public class MockVideoReceiver : MonoBehaviour
{
    // Singleton so the UI can find US, instead of us finding the UI
    public static MockVideoReceiver Instance;

    [Header("Network Simulation")]
    public float connectionLatency = 0.5f; 

    [Header("Video Source")]
    public VideoClip mockVideoFile; 

    // --- THE SOURCE OF TRUTH ---
    public Texture CurrentFeed { get; private set; }
    public event Action<Texture> OnStreamUpdated; // UI listens to this

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

    void Start()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnActiveSlotChanged += OnSlotChanged;
            OnSlotChanged(SelectionManager.Instance.ActiveSlotId);
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnActiveSlotChanged -= OnSlotChanged;
    }

    void OnSlotChanged(int slotId)
    {
        if (SelectionManager.Instance == null) return;

        string newDroneId = SelectionManager.Instance.GetDroneAtSlot(slotId);

        if (!string.IsNullOrEmpty(newDroneId))
        {
            currentDroneId = newDroneId;
            StopAllCoroutines();
            StartCoroutine(SimulateConnection());
        }
        else
        {
            Disconnect();
        }
    }

    IEnumerator SimulateConnection()
    {
        yield return new WaitForSeconds(connectionLatency);

        if (mockVideoFile != null)
        {
            videoPlayer.clip = mockVideoFile;
            videoPlayer.Prepare();

            while (!videoPlayer.isPrepared) yield return null;

            videoPlayer.Play();
            yield return null; 

            // ✅ UPDATE STATE
            CurrentFeed = videoPlayer.texture;
            
            // ✅ NOTIFY LISTENERS (Safely)
            Debug.Log($"✅ MockReceiver: Stream Started for {currentDroneId}");
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
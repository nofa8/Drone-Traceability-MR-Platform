using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(AspectRatioFitter))] 
public class POVPanelController : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("-1 = Follow Active Slot. 0, 1 = Lock to specific slot.")]
    public int boundSlotId = -1;

    [Header("UI Components")]
    public RawImage videoDisplay;      // The Screen
    public TextMeshProUGUI statusText; // "LIVE", "NO SIGNAL"
    public Image recordingIcon;        // Red dot (optional)

    [Header("Simulation Source (Fallback)")]
    public Texture offlinePlaceholder; // Static/Noise image
    public RenderTexture simulatedFeed; // The cable from Step 1

    private AspectRatioFitter ratioFitter;
    private int currentTargetSlot = -1;

    void Awake()
    {
        ratioFitter = GetComponent<AspectRatioFitter>();
        // Default to Fit Inside Parent (so we don't stretch weirdly)
        if (ratioFitter) ratioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
    }

    void OnEnable()
    {
        // 1. Subscribe to Slot Changes
        if (SelectionManager.Instance != null)
        {
            if (boundSlotId == -1)
            {
                SelectionManager.Instance.OnActiveSlotChanged += RefreshConnection;
                RefreshConnection(SelectionManager.Instance.ActiveSlotId);
            }
            else
            {
                RefreshConnection(boundSlotId);
            }
        }
        else
        {
            RefreshConnection(0); // Fallback
        }
    }

    void OnDisable()
    {
        if (SelectionManager.Instance != null && boundSlotId == -1)
        {
            SelectionManager.Instance.OnActiveSlotChanged -= RefreshConnection;
        }
        Disconnect();
    }

    // --- LOGIC: CONNECTION ---

    void RefreshConnection(int slotId)
    {
        currentTargetSlot = slotId;

        // PHASE 5: MOCK LOGIC
        // In the future, this is where you ask "GetWebRTCFeedFor(slotId)"
        // For now, we check if the slot has a drone, and show the Sim Feed.
        
        string droneId = SelectionManager.Instance ? SelectionManager.Instance.GetDroneAtSlot(slotId) : null;

        if (!string.IsNullOrEmpty(droneId))
        {
            ConnectToMockStream();
        }
        else
        {
            Disconnect();
        }
    }

    void ConnectToMockStream()
    {
        if (simulatedFeed != null)
        {
            //Debug.Log($"🎥 POV (Slot {currentTargetSlot}): Connected to Simulated Feed");
            SetVideoTexture(simulatedFeed);
        }
        else
        {
            Debug.LogWarning("⚠️ POV: No Simulated Feed assigned!");
            SetStatus("NO SOURCE", false);
        }
    }

    public void Disconnect()
    {
        if (videoDisplay != null)
        {
            videoDisplay.texture = offlinePlaceholder;
        }
        SetStatus("OFFLINE", false);
    }

    // --- PUBLIC API (WebRTC Entry Point) ---

    public void SetVideoTexture(Texture newFeed)
    {
        if (videoDisplay != null && newFeed != null)
        {
            videoDisplay.texture = newFeed;
            
            // Auto-adjust Aspect Ratio
            if (ratioFitter != null)
            {
                float ratio = (float)newFeed.width / newFeed.height;
                ratioFitter.aspectRatio = ratio;
            }

            SetStatus("LIVE FEED", true);
        }
    }

    private void SetStatus(string text, bool active)
    {
        if (statusText) 
        {
            statusText.text = text;
            statusText.color = active ? Color.green : Color.red;
        }
        if (recordingIcon) recordingIcon.gameObject.SetActive(active);
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(AspectRatioFitter))]
public class POVPanelController : MonoBehaviour
{
    // Static instance is still useful, but not critical anymore
    public static POVPanelController Instance; 

    [Header("UI Components")]
    public RawImage videoDisplay;      
    public TextMeshProUGUI statusText; 
    public Image recordingIcon;        

    [Header("Defaults")]
    public Texture offlinePlaceholder; 
    public Color noSignalColor = Color.gray;

    private AspectRatioFitter ratioFitter;

    void Awake()
    {
        Instance = this;
        ratioFitter = GetComponent<AspectRatioFitter>();
        if (ratioFitter) ratioFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
    }

    // 🔥 THIS IS THE FIX
    void OnEnable()
    {
        // 1. Check if the receiver is already running a stream
        if (MockVideoReceiver.Instance != null)
        {
            // Subscribe for future updates
            MockVideoReceiver.Instance.OnStreamUpdated += HandleStreamUpdate;

            // Grab the CURRENT stream immediately (in case it started while we were closed)
            if (MockVideoReceiver.Instance.CurrentFeed != null)
            {
                HandleStreamUpdate(MockVideoReceiver.Instance.CurrentFeed);
            }
            else
            {
                Disconnect();
            }
        }
        else
        {
            Disconnect();
        }
    }

    void OnDisable()
    {
        // Clean up subscription so we don't get errors when closed
        if (MockVideoReceiver.Instance != null)
        {
            MockVideoReceiver.Instance.OnStreamUpdated -= HandleStreamUpdate;
        }
    }

    // Event Handler
    void HandleStreamUpdate(Texture newFeed)
    {
        if (newFeed != null) SetVideoTexture(newFeed);
        else Disconnect();
    }

    // --- VISUAL LOGIC ---

    public void SetVideoTexture(Texture newFeed)
    {
        if (videoDisplay != null && newFeed != null)
        {
            videoDisplay.texture = newFeed;
            videoDisplay.color = Color.white; 
            
            if (ratioFitter != null)
            {
                float ratio = (float)newFeed.width / newFeed.height;
                ratioFitter.aspectRatio = ratio;
            }

            SetStatus("LIVE FEED", true);
        }
    }

    public void Disconnect()
    {
        if (videoDisplay != null)
        {
            if (offlinePlaceholder != null)
            {
                videoDisplay.texture = offlinePlaceholder;
                videoDisplay.color = Color.white;
            }
            else
            {
                videoDisplay.texture = null;
                videoDisplay.color = noSignalColor;
            }
        }
        SetStatus("NO SIGNAL", false);
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
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// POV Panel Controller - WebRTC Only.
/// Displays drone video feeds with status indicators.
/// </summary>
[RequireComponent(typeof(AspectRatioFitter))]
public class POVPanelController : MonoBehaviour
{
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

    void OnEnable()
    {
        // Subscribe to VideoStreamController
        if (VideoStreamController.Instance != null)
        {
            VideoStreamController.Instance.OnVideoReceived += HandleVideoReceived;
            VideoStreamController.Instance.OnStatusChanged += HandleStatusChanged;

            // Grab current state
            if (VideoStreamController.Instance.CurrentTexture != null)
                HandleVideoReceived(VideoStreamController.Instance.CurrentTexture);
            else
                HandleStatusChanged(VideoStreamController.Instance.CurrentStatus);
        }
        else
        {
            Disconnect();
        }
    }

    void OnDisable()
    {
        if (VideoStreamController.Instance != null)
        {
            VideoStreamController.Instance.OnVideoReceived -= HandleVideoReceived;
            VideoStreamController.Instance.OnStatusChanged -= HandleStatusChanged;
        }
    }

    // --- EVENT HANDLERS ---

    void HandleVideoReceived(Texture texture)
    {
        if (texture != null)
            SetVideoTexture(texture);
    }

    void HandleStatusChanged(VideoStreamController.StreamStatus status)
    {
        switch (status)
        {
            case VideoStreamController.StreamStatus.Live:
                SetStatus("LIVE", true, Color.green);
                break;
            case VideoStreamController.StreamStatus.Connecting:
                SetStatus("CONNECTING...", false, Color.white);
                break;
            case VideoStreamController.StreamStatus.Failed:
                SetStatus("FAILED", false, Color.red);
                break;
            case VideoStreamController.StreamStatus.Offline:
            default:
                Disconnect();
                break;
        }
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
        SetStatus("NO SIGNAL", false, Color.red);
    }

    private void SetStatus(string text, bool active, Color color)
    {
        if (statusText)
        {
            statusText.text = text;
            statusText.color = color;
        }
        if (recordingIcon) recordingIcon.gameObject.SetActive(active);
    }
}
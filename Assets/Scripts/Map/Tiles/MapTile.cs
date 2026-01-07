using UnityEngine;
using UnityEngine.UI;

public class MapTile
{
    public int x;
    public int y;
    public int zoom;
    
    // The visual representation (In our UI system, this is a RectTransform with a RawImage)
    public GameObject gameObject;
    public RawImage rawImage;
    public RectTransform rectTransform;
    
    public bool IsTextureLoaded = false;

    public MapTile(int x, int y, int zoom, GameObject obj)
    {
        this.x = x;
        this.y = y;
        this.zoom = zoom;
        this.gameObject = obj;
        this.rawImage = obj.GetComponent<RawImage>();
        this.rectTransform = obj.GetComponent<RectTransform>();
    }
}
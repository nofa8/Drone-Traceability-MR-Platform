using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(RectTransform))]
public class MapTileRenderer : MonoBehaviour
{
    [Header("Configuration")]
    public int cacheSize = 64;
    public bool showDebugGrid = false;

    [Header("Container")]
    public Transform tileContainer; // Parent for the tiles (should be behind markers)
    public GameObject tilePrefab;   // Prefab with RawImage

    // Architecture Components
    private IMapTileProvider tileProvider;
    private GeoMapContext mapContext;

    // State
    private Dictionary<string, MapTile> activeTiles = new Dictionary<string, MapTile>();
    private Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private Dictionary<string, UnityWebRequest> activeRequests = new Dictionary<string, UnityWebRequest>();
    private int currentZoom = 15;

    // --- LIFECYCLE ---

    void Start()
    {
        // 1. Init Provider (Dependency Injection point)
        tileProvider = new OSMTileProvider();

        // 2. Hook into Truth (GeoMapContext)
        mapContext = GeoMapContext.Instance;
        if (mapContext != null)
        {
            mapContext.OnMapUpdated += RefreshTiles;
            RefreshTiles(); // Initial Draw
        }
    }

    void OnDestroy()
    {
        if (mapContext != null)
            mapContext.OnMapUpdated -= RefreshTiles;
    }

    // --- CORE LOOP ---

    public void RefreshTiles()
    {
        if (!mapContext || !tileContainer) return;

        // 1. Calculate Zoom Level based on Context Scale
        // Formula: Resolution = 156543.03 * cos(lat) / 2^zoom
        // Therefore: 2^zoom = 156543.03 * cos(lat) * pixelsPerMeter
        double latRad = mapContext.originLat * Mathf.Deg2Rad;
        double val = 156543.03 * Math.Cos(latRad) * mapContext.pixelsPerMeter;
        currentZoom = Mathf.FloorToInt((float)(Math.Log(val) / Math.Log(2)));
        currentZoom = Mathf.Clamp(currentZoom, 0, tileProvider.MaxZoom);

        // 2. Determine Visible Bounds in Tiles
        // We calculate the Center Tile, then fan out based on screen size
        Vector2 centerTileCoords = GeoToTile(mapContext.originLat, mapContext.originLon, currentZoom);
        
        // How many tiles fit on screen? (Screen Size / Tile Size (256))
        // We add a buffer to load tiles just outside view
        RectTransform mapRect = GetComponent<RectTransform>();
        int rangeX = Mathf.CeilToInt(mapRect.rect.width / 256f / 2f) + 1;
        int rangeY = Mathf.CeilToInt(mapRect.rect.height / 256f / 2f) + 1;

        int minX = (int)centerTileCoords.x - rangeX;
        int maxX = (int)centerTileCoords.x + rangeX;
        int minY = (int)centerTileCoords.y - rangeY;
        int maxY = (int)centerTileCoords.y + rangeY;

        // 3. Reconcile Tiles (Load New, Unload Old)
        HashSet<string> neededKeys = new HashSet<string>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                string key = $"{currentZoom}_{x}_{y}";
                neededKeys.Add(key);

                if (!activeTiles.ContainsKey(key))
                {
                    SpawnTile(x, y, currentZoom, key);
                }
            }
        }

        CleanupTiles(neededKeys);
        
        // 4. Update Positions (Alignment)
        UpdateTileTransforms();
    }

    // --- TILE MATH (SLIPPY MAP) ---

    private Vector2 GeoToTile(double lat, double lon, int zoom)
    {
        int n = 1 << zoom; // 2^zoom
        double x = n * ((lon + 180.0) / 360.0);
        double latRad = lat * Math.PI / 180.0;
        double y = n * (1.0 - (Math.Log(Math.Tan(latRad) + (1.0 / Math.Cos(latRad))) / Math.PI)) / 2.0;
        return new Vector2((float)x, (float)y);
    }

    private Vector2 TileToGeo(int x, int y, int zoom)
    {
        int n = 1 << zoom;
        double lon = (x / (double)n) * 360.0 - 180.0;
        double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * y / (double)n)));
        double lat = latRad * 180.0 / Math.PI;
        return new Vector2((float)lon, (float)lat); // Top-Left corner of tile
    }

    // --- RENDERING ---

    private void SpawnTile(int x, int y, int zoom, string key)
    {
        GameObject obj = Instantiate(tilePrefab, tileContainer);
        obj.name = key;

        MapTile tile = new MapTile(x, y, zoom, obj);
        activeTiles.Add(key, tile);

        StartCoroutine(LoadTexture(tile, key));
    }

    private void UpdateTileTransforms()
    {
        // We position tiles relative to the Map Center (GeoMapContext Origin)
        // This ensures they stick to the map logic.

        // 1. Get Origin in Tile Coordinates (Fractional)
        Vector2 originTilePos = GeoToTile(mapContext.originLat, mapContext.originLon, currentZoom);

        foreach (var tile in activeTiles.Values)
        {
            // 2. Calculate Difference
            float diffX = tile.x - originTilePos.x;
            float diffY = tile.y - originTilePos.y; // Y increases downwards in Tile System

            // 3. Convert to Screen Pixels (256px per tile standard)
            // Note: Since 'pixelsPerMeter' scales with Zoom, we need to apply the specific scale factor
            // Logic: At this specific zoom level, 1 Tile = 256 "Native" Map Pixels.
            // But we might be "Between" zoom levels in the UI (smooth zoom).
            // So we scale the tile to match the ACTUAL pixelsPerMeter.
            
            // Standard resolution at this integer zoom:
            double latRad = mapContext.originLat * Mathf.Deg2Rad;
            double metersPerPixelAtZoom = 156543.03 * Math.Cos(latRad) / Math.Pow(2, currentZoom);
            float tileScale = (float)((256.0 * metersPerPixelAtZoom) * mapContext.pixelsPerMeter) / 256.0f;

            // 4. Position
            // Unity UI (0,0) is Center. 
            // Tile (x,y) is Top-Left. 
            // Distance in tiles * 256 * Scale
            
            float posX = diffX * 256f * tileScale;
            float posY = -diffY * 256f * tileScale; // Invert Y for Unity UI

            tile.rectTransform.anchoredPosition = new Vector2(posX, posY);
            tile.rectTransform.localScale = Vector3.one * tileScale;
            
            // Debug alignment
            if (showDebugGrid) tile.rawImage.color = ((tile.x + tile.y) % 2 == 0) ? Color.white : new Color(0.9f, 0.9f, 0.9f);
        }
    }

    private IEnumerator LoadTexture(MapTile tile, string key)
    {
        if (textureCache.ContainsKey(key))
        {
            tile.rawImage.texture = textureCache[key];
            tile.IsTextureLoaded = true;
            yield break;
        }

        string url = tileProvider.GetTileUrl(tile.x, tile.y, tile.zoom);
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            // 🔥 INSERT THIS: Register the request
            if (activeRequests.ContainsKey(key)) activeRequests[key].Abort();
            activeRequests[key] = uwr;

            uwr.SetRequestHeader("User-Agent", "UnityDroneGCS/1.0");
            yield return uwr.SendWebRequest();

            // 🔥 INSERT THIS: Unregister when done
            if (activeRequests.ContainsKey(key)) activeRequests.Remove(key);

            if (uwr.result == UnityWebRequest.Result.Success && tile.gameObject != null)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(uwr);
                
                // (Your existing Cache Logic...)
                if (textureCache.Count > cacheSize) 
                {
                     var enumerator = textureCache.Keys.GetEnumerator();
                     if (enumerator.MoveNext()) textureCache.Remove(enumerator.Current);
                }
                textureCache[key] = tex;
                tile.rawImage.texture = tex;
                tile.IsTextureLoaded = true;
            }
        }
    }

    private void CleanupTiles(HashSet<string> neededKeys)
    {
        List<string> toRemove = new List<string>();
        foreach (var key in activeTiles.Keys)
        {
            if (!neededKeys.Contains(key)) toRemove.Add(key);
        }

        foreach (var key in toRemove)
        {
            if (activeRequests.ContainsKey(key))
            {
                activeRequests[key].Abort();
                activeRequests.Remove(key);
            }

            Destroy(activeTiles[key].gameObject);
            activeTiles.Remove(key);
        }
    }
}
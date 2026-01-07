using UnityEngine;

public class OSMTileProvider : IMapTileProvider
{
    public int MaxZoom => 19;
    public string Attribution => "© OpenStreetMap contributors";

    public string GetTileUrl(int x, int y, int zoom)
    {
        // Standard OSM Slippy Map URL
        return $"https://tile.openstreetmap.org/{zoom}/{x}/{y}.png";
    }
}
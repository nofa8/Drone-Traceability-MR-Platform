public interface IMapTileProvider
{
    /// <summary>
    /// Returns the URL for a specific tile.
    /// </summary>
    string GetTileUrl(int x, int y, int zoom);

    /// <summary>
    /// The maximum zoom level supported by this provider.
    /// </summary>
    int MaxZoom { get; }
    
    /// <summary>
    /// Attribution text (Required by OSM/Mapbox).
    /// </summary>
    string Attribution { get; }
}
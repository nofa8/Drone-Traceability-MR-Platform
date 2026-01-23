using System;

/// <summary>
/// Interface for components that need to reconnect when network configuration changes.
/// Implements Dependency Inversion: SettingsPanelController depends on abstraction, not concrete classes.
/// </summary>
public interface INetworkReconfigurable
{
    /// <summary>
    /// Called when NetworkConfig values change. Component should reconnect using new values.
    /// </summary>
    void OnNetworkConfigChanged();
    
    /// <summary>
    /// Human-readable name for logging/debugging.
    /// </summary>
    string ServiceName { get; }
}

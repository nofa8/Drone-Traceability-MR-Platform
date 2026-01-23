using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Centralized network configuration with Quest-safe persistence.
/// Simplified: Single IP address, fixed ports.
/// </summary>
[CreateAssetMenu(fileName = "NetworkConfig", menuName = "Drone Platform/Network Config")]
public class NetworkConfig : ScriptableObject
{
    // === SINGLETON ===
    private static NetworkConfig _instance;
    public static NetworkConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<NetworkConfig>("NetworkConfig");
                if (_instance == null)
                {
                    Debug.LogWarning("[NetworkConfig] No asset found in Resources. Creating runtime instance.");
                    _instance = CreateInstance<NetworkConfig>();
                }
                _instance.LoadFromDevice();
            }
            return _instance;
        }
    }

    // === CONFIGURATION FIELDS ===
    [Header("Server IP")]
    [Tooltip("IP address of the server (e.g., 192.168.1.100)")]
    public string serverIP = "192.168.200.2";

    [Header("Ports (Fixed)")]
    [Tooltip("WebSocket port for telemetry")]
    public int websocketPort = 8080;
    
    [Tooltip("WebRTC/WHEP port for video")]
    public int webrtcPort = 8889;

    // === COMPUTED URLs ===
    /// <summary>
    /// Full WebSocket URL constructed from serverIP and port.
    /// </summary>
    public string WebSocketUrl => $"ws://{serverIP}:{websocketPort}/";
    
    /// <summary>
    /// WebRTC server IP (same as serverIP).
    /// </summary>
    public string WebRTCServerIP => serverIP;
    
    /// <summary>
    /// WebRTC port.
    /// </summary>
    public int WebRTCPort => webrtcPort;

    // === EVENTS ===
    public static event Action OnConfigChanged;

    // === REGISTERED SERVICES ===
    private static readonly List<INetworkReconfigurable> _services = new();

    public static void RegisterService(INetworkReconfigurable service)
    {
        if (!_services.Contains(service))
        {
            _services.Add(service);
            Debug.Log($"[NetworkConfig] Registered: {service.ServiceName}");
        }
    }

    public static void UnregisterService(INetworkReconfigurable service)
    {
        _services.Remove(service);
    }

    // === PERSISTENCE ===
    // Bump this version whenever you change the default IP/ports in code
    // to force all devices to use the new defaults
    private const int CURRENT_VERSION = 3;
    private static string ConfigPath => Path.Combine(Application.persistentDataPath, "network_config.json");
    private static string BackupPath => ConfigPath + ".backup";

    [Serializable]
    private class ConfigData
    {
        public int version = CURRENT_VERSION;
        public string serverIP;
        public int websocketPort;
        public int webrtcPort;
    }

    public void LoadFromDevice()
    {
        if (!File.Exists(ConfigPath))
        {
            Debug.Log("[NetworkConfig] No saved config found, using defaults.");
            return;
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            var data = JsonUtility.FromJson<ConfigData>(json);

            if (data == null)
            {
                Debug.LogWarning("[NetworkConfig] Failed to parse config, using defaults.");
                return;
            }

            // Version migration - if version is old, migrate and use code defaults
            if (data.version < CURRENT_VERSION)
            {
                Debug.Log($"[NetworkConfig] Migrating from v{data.version} to v{CURRENT_VERSION}");
                MigrateConfig(data);
                // Don't apply old values - use code defaults instead
                Debug.Log($"[NetworkConfig] Using code defaults: IP={serverIP}, WS Port={websocketPort}, WebRTC Port={webrtcPort}");
                return;
            }

            // Only apply loaded values if version matches (no migration needed)
            serverIP = data.serverIP ?? serverIP;
            websocketPort = data.websocketPort > 0 ? data.websocketPort : websocketPort;
            webrtcPort = data.webrtcPort > 0 ? data.webrtcPort : webrtcPort;

            Debug.Log($"[NetworkConfig] Loaded: IP={serverIP}, WS Port={websocketPort}, WebRTC Port={webrtcPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkConfig] Load failed: {e.Message}. Trying backup...");
            TryRestoreBackup();
        }
    }

    public void SaveToDevice()
    {
        var data = new ConfigData
        {
            version = CURRENT_VERSION,
            serverIP = serverIP,
            websocketPort = websocketPort,
            webrtcPort = webrtcPort
        };

        string json = JsonUtility.ToJson(data, true);
        string tempPath = ConfigPath + ".tmp";

        try
        {
            File.WriteAllText(tempPath, json);

            if (File.Exists(ConfigPath))
            {
                File.Copy(ConfigPath, BackupPath, overwrite: true);
            }

            if (File.Exists(ConfigPath))
                File.Delete(ConfigPath);
            File.Move(tempPath, ConfigPath);

            Debug.Log($"[NetworkConfig] Saved to {ConfigPath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkConfig] Save failed: {e.Message}");
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public void ApplyAndReconnect()
    {
        SaveToDevice();
        
        Debug.Log($"[NetworkConfig] Notifying {_services.Count} services to reconnect...");
        foreach (var service in _services)
        {
            try
            {
                Debug.Log($"[NetworkConfig] → Reconnecting: {service.ServiceName}");
                service.OnNetworkConfigChanged();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkConfig] {service.ServiceName} reconnect failed: {e.Message}");
            }
        }

        OnConfigChanged?.Invoke();
    }

    private void MigrateConfig(ConfigData data)
    {
        // When version changes, we want to use the NEW defaults from code
        // So we DON'T copy old values - just update the version
        // The calling code will skip applying old values when migration happens
        Debug.Log($"[NetworkConfig] Migration: Resetting to code defaults (IP={serverIP}, WS={websocketPort}, WebRTC={webrtcPort})");
        data.version = CURRENT_VERSION;
        
        // Save the new defaults immediately
        SaveToDevice();
    }

    private void TryRestoreBackup()
    {
        if (!File.Exists(BackupPath))
        {
            Debug.LogWarning("[NetworkConfig] No backup available.");
            return;
        }

        try
        {
            File.Copy(BackupPath, ConfigPath, overwrite: true);
            Debug.Log("[NetworkConfig] Restored from backup. Retrying load...");
            LoadFromDevice();
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkConfig] Backup restore failed: {e.Message}");
        }
    }

    // === VALIDATION ===
    public bool ValidateIP(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        return System.Net.IPAddress.TryParse(ip, out _);
    }
}

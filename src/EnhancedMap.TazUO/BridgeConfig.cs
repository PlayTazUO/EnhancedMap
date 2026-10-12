using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assistant;

/// <summary>
///     Persisted connection settings for the EnhancedMap server. Stored as JSON next to the
///     plugin assembly (i.e. TazUO's Data/Plugins folder).
/// </summary>
public sealed class BridgeConfig
{
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; } = 8887;
    public string Username { get; set; } = "You";
    public string Password { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(Address) && Port is > 0 and <= 65535;

    [JsonIgnore]
    public static string FilePath =>
        Path.Combine(
            Path.GetDirectoryName(typeof(BridgeConfig).Assembly.Location) ?? AppContext.BaseDirectory,
            "EnhancedMapBridge.json");

    public static BridgeConfig Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var cfg = JsonSerializer.Deserialize<BridgeConfig>(File.ReadAllText(FilePath));
                if (cfg != null)
                    return cfg;
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Could not read config ({FilePath}): {e.Message}");
        }

        return new BridgeConfig();
    }

    public bool Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions {WriteIndented = true}));
            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Could not save config ({FilePath}): {e.Message}");
            return false;
        }
    }
}

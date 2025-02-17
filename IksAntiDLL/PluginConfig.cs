using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace IksAntiDLL;

public class PluginConfig : IBasePluginConfig
{    
    [JsonPropertyName("Ban")]
    public bool Ban { get; set; } = false;
    
    [JsonPropertyName("Reason")]
    public string Reason { get; set; } = "[AntiDLL] Detected";
    
    [JsonPropertyName("Duration")]
    public int Duration { get; set; } = 0;
    
    [JsonPropertyName("Version")]
    public int Version { get; set; } = 1;
}
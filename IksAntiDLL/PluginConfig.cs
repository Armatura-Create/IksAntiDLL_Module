using CounterStrikeSharp.API.Core;

namespace IksAntiDLL;

public class PluginConfig : BasePluginConfig
{
    public bool Ban { get; set; } = false;
    public string Reason { get; set; } = "[AntiDLL] Detected";
    public int Duration { get; set; } = 43200;
}
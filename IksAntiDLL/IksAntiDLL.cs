using System.Collections.Concurrent;
using AntiDLL.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using IksAdminApi;
using Microsoft.Extensions.Logging;

namespace IksAntiDLL;

public class IksAntiDLL : AdminModule, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "IksAntiDLL";
    public override string ModuleAuthor => "Armatura";
    public override string ModuleVersion => "1.0.0";

    public PluginConfig Config { get; set; }

    private static readonly ConcurrentDictionary<ulong, DateTime> LastDetectionTime = new();
    private static readonly TimeSpan DetectionCooldown = TimeSpan.FromSeconds(10);

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;

        if (Config.Reason.Length == 0)
        {
            Config.Reason = "[AntiDLL] Detected";
        }

        Logger.LogInformation("[IksAntiDLL] Config parsed!");
    }

    private static PluginCapability<IAntiDLL> AntiDLL { get; } = new("AntiDLL");

    private void OnDetection(CCSPlayerController player, string eventName)
    {
        if (player.AuthorizedSteamID == null) return;

        var steamId = player.AuthorizedSteamID.SteamId64;
        var now = DateTime.UtcNow;

        if (LastDetectionTime.TryGetValue(steamId, out var lastTime))
        {
            if ((now - lastTime) < DetectionCooldown)
            {
                return;
            }
        }

        LastDetectionTime[steamId] = now;

        if (Config.Ban)
        {
            var playerBan = new PlayerBan(steamId.ToString(), player.IpAddress, player.PlayerName, Config.Reason, Config.Duration)
            {
                AdminId = Api.ConsoleAdmin.Id,
                CreatedAt = AdminUtils.CurrentTimestamp(),
                UpdatedAt = AdminUtils.CurrentTimestamp()
            };

            Logger.LogInformation("[IksAntiDLL] Banning player {0}, event - {1}", player.PlayerName, eventName);
            Api.AddBan(playerBan);
        }
        else
        {
            Logger.LogInformation("[IksAntiDLL] Kicking player {0}, event - {1}", player.PlayerName, eventName);
            Api.Kick(Api.ConsoleAdmin, player, Config.Reason);
        }
    }

    public override void Ready()
    {
        var antidll = AntiDLL.Get();
        if (antidll == null) return;

        Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromHours(1));
                LastDetectionTime.Clear();
            }
        });

        Logger.LogInformation("[IksAntiDLL] is ready");
        antidll.OnDetection += OnDetection;
    }

    public override void Unload(bool hotReload)
    {
        var antidll = AntiDLL.Get();
        if (antidll == null) return;

        antidll.OnDetection -= OnDetection;
    }
}
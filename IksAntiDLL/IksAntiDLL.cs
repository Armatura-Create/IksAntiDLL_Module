using AntiDLL.API;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using IksAdminApi;
using Microsoft.Extensions.Logging;

namespace IksAntiDLL;

public class IksAntiDLL : AdminModule, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "IksAntiDLL";
    public override string ModuleAuthor => "Armatura";
    public override string ModuleVersion => "1.0.1";

    public PluginConfig Config { get; set; }

    private static readonly Dictionary<ulong, DateTime> LastDetectionTime = new();
    private static readonly object LockObject = new();
    private static readonly TimeSpan DetectionCooldown = TimeSpan.FromSeconds(10);

    private static readonly Dictionary<ulong, CCSPlayerController> PlayersToDisconnect = new();

    private static PluginCapability<IAntiDLL> AntiDLL { get; } = new("AntiDLL");

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;

        if (Config.Reason.Length == 0)
        {
            Config.Reason = "[AntiDLL] Detected";
        }

        if (Config.Duration < 0)
        {
            Config.Duration = 0;
        }

        Logger.LogInformation("[IksAntiDLL] Config parsed!");
    }

    private void OnDetection(CCSPlayerController player, string eventName)
    {
        if (player.AuthorizedSteamID == null) return;

        var steamId = player.AuthorizedSteamID.SteamId64;
        var now = DateTime.UtcNow;

        lock (LockObject)
        {
            if (LastDetectionTime.TryGetValue(steamId, out var lastTime) && (now - lastTime) < DetectionCooldown)
            {
                return;
            }

            LastDetectionTime[steamId] = now;
        }

        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));

            Server.NextWorldUpdate(() =>
            {
                if (Config.Ban)
                {
                    var playerBan = new PlayerBan(steamId.ToString(), player.IpAddress, player.PlayerName,
                        Config.Reason,
                        Config.Duration)
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
                }

                PlayersToDisconnect[steamId] = player;
            });
        });
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        var player = @event?.Userid;
        if (player is not { IsBot: false, IsValid: true } || player.AuthorizedSteamID == null)
            return HookResult.Continue;

        lock (LockObject)
        {
            PlayersToDisconnect.Remove(player.AuthorizedSteamID.SteamId64);
            LastDetectionTime.Remove(player.AuthorizedSteamID.SteamId64);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerFullConnect(EventPlayerConnectFull @event, GameEventInfo info)
    {
        var player = @event?.Userid;
        if (player is not { IsBot: false, IsValid: true } || player.AuthorizedSteamID == null)
            return HookResult.Continue;

        PlayersToDisconnect.TryGetValue(player.AuthorizedSteamID.SteamId64, out var playerToDisconnect);

        if (playerToDisconnect != null)
        {
            Task.Run(() =>
            {
                player.Disconnect(Config.Ban
                    ? NetworkDisconnectionReason.NETWORK_DISCONNECT_STEAM_BANNED
                    : NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
                return Task.CompletedTask;
            });
        }

        return HookResult.Continue;
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
        Instance.RegisterEventHandler<EventPlayerConnectFull>(OnPlayerFullConnect);
        Instance.RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        antidll.OnDetection += OnDetection;
    }

    public override void Unload(bool hotReload)
    {
        var antidll = AntiDLL.Get();
        if (antidll == null) return;

        antidll.OnDetection -= OnDetection;
    }
}
using SyncKit.Bot;

namespace EggLedger.Web.Server.Bot;

public sealed class EggLedgerBotHostedService(BotConfig config, ILogger<EggLedgerBotHostedService> logger) : IHostedService {
    public SyncKitBot? Bot { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken) {
        try {
            Bot = await SyncKitBot.StartAsync(config);
        } catch (Exception ex) {
            logger.LogWarning(ex, "synckit: bot start failed, continuing");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        if (Bot is not null) {
            await Bot.DisposeAsync();
        }
    }
}

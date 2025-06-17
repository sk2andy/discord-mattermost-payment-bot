using discord_payment_bot.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace discord_payment_bot.Services;

public class BotService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly string _botToken;
    private readonly ILogger<BotService> _logger;

    public BotService(
        DiscordSocketClient client,
        IOptions<BotOptions> botOptions, ILogger<BotService> logger)
    {
        _client = client;
        _botToken = botOptions.Value.BotToken;
        _logger = logger;

        // Optional: Event-Handler for logging
        _client.Log += message => {
            _logger.LogInformation(message.ToString());
            return Task.CompletedTask;
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting bot...");
        // Login und Start des Bots
        await _client.LoginAsync(TokenType.Bot, _botToken);
        await _client.StartAsync();
        
        var tcs = new TaskCompletionSource<bool>();
        _client.Ready += () => {
            _logger.LogInformation("Bot sucessfully logged in and ready to accept connections...");
            tcs.SetResult(true);
            return Task.CompletedTask;
        };
        await tcs.Task;
        
        try 
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (TaskCanceledException) 
        {
            // Graceful shutdown
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping discord bot...");
        await _client.LogoutAsync();
        await _client.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}
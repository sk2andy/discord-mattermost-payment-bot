using discord_payment_bot.Models;
using discord_payment_bot.Services.Orchestration;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace discord_payment_bot.Services;

public class BotService : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly RegistrationOrchestrator _registrationOrchestrator;
    private readonly string _botToken;
    private readonly ILogger<BotService> _logger;
    private readonly ulong _guildId;

    public BotService(
        DiscordSocketClient client,
        RegistrationOrchestrator registrationOrchestrator,
        IOptions<BotOptions> botOptions, ILogger<BotService> logger)
    {
        _client = client;
        _registrationOrchestrator = registrationOrchestrator;
        _botToken = botOptions.Value.BotToken;
        _guildId = botOptions.Value.GuildId;
        _logger = logger;
        
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

        // Create slash command for Mattermost registration in a specific guild
        var guild = _client.GetGuild(_guildId);
        var registerCommand = new SlashCommandBuilder()
            .WithName("register")
            .WithDescription("Complete mattermost registration")
            .AddOption("email", ApplicationCommandOptionType.String, "Your Mattermost email address", isRequired: true)
            .AddOption("username", ApplicationCommandOptionType.String, "Your Mattermost username", isRequired: true)
            .AddOption("password", ApplicationCommandOptionType.String, "Your Mattermost password", isRequired: true)
            .Build();
        await _client.Rest.CreateGuildCommand(registerCommand, guild.Id);
        
        _client.SlashCommandExecuted += SlashCommandHandler;
        
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

    /// <summary>
    /// Handles incoming interactions: slash commands and modal submissions.
    /// </summary>
    private async Task SlashCommandHandler(SocketSlashCommand slash)
    {
        if (slash.Data.Name == "register")
        {
            // Extract input values
            var email = slash.Data.Options.FirstOrDefault(o => o.Name == "email")?.Value?.ToString();
            var username = slash.Data.Options.FirstOrDefault(o => o.Name == "username")?.Value?.ToString();
            var password = slash.Data.Options.FirstOrDefault(o => o.Name == "password")?.Value?.ToString();
            // ToDo validate email and username
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                await slash.RespondAsync("Please provide email, username and password.", ephemeral: true);
                return;
            }

            await slash.RespondAsync("Thank you for your registration! We will process your request shortly.",
                ephemeral: true);
            await _registrationOrchestrator.FinishRegisterUserAsync(slash.User.Id, email, username, password);
        }
    }

    public override void Dispose()
    {
        _client.SlashCommandExecuted -= SlashCommandHandler;
        base.Dispose();
    }
}
using discord_payment_bot.Models;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace discord_payment_bot.Services.Discord;

public class DiscordMessageSender
{
    private readonly DiscordSocketClient _discordSocketClient;
    private readonly ILogger<DiscordMessageSender> _logger;
    private readonly BotOptions _options;

    public DiscordMessageSender(
        DiscordSocketClient discordSocketClient,
        IOptions<BotOptions> botOptions,
        ILogger<DiscordMessageSender> logger)
    {
        _discordSocketClient = discordSocketClient;
        _logger = logger;
        _options = botOptions.Value;
    }
    
    public async Task SendDirectMessageAsync(ulong userId, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var guild = _discordSocketClient.GetGuild(_options.GuildId) ?? throw new InvalidOperationException($"Guild with ID {_options.GuildId} not found.");
            
            var user = guild.GetUser(userId) ?? throw new InvalidOperationException($"User with ID {userId} not found in guild {_options.GuildId}.");
            await user.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to channel {UserId}", userId);
        }
    }
    
    public async Task SendModChannelMessageAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _discordSocketClient.GetChannelAsync(_options.ModChannelId) is not ISocketMessageChannel channel)
            {
                _logger.LogError("Mod channel with ID {ModChannelId} not found.", _options.ModChannelId);
                throw new InvalidOperationException($"Channel with ID {_options.ModChannelId} not found.");
            }
            
            await channel.SendMessageAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to mod channel {ModChannelId}", _options.ModChannelId);
        }
    }
}
using discord_payment_bot.Models;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace discord_payment_bot.Services.Discord;

public class DiscordGetUserService
{
    private readonly DiscordSocketClient _discordSocketClient;
    private readonly ILogger<DiscordGetUserService> _logger;

    public DiscordGetUserService(
        IOptions<BotOptions> botOptions,
        DiscordSocketClient discordSocketClient,
        ILogger<DiscordGetUserService> logger)
    {
        _discordSocketClient = discordSocketClient;
        _logger = logger;
    }
    
    public async Task<SocketGuildUser?> GetUserAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var guild = _discordSocketClient.GetGuild(_discordSocketClient.Guilds.FirstOrDefault()?.Id ?? 0);
            if (guild == null)
            {
                _logger.LogError("Discord guild not found.");
                return null;
            }

            var user = guild.GetUser(userId);
            
            if (user == null)
            {
                _logger.LogError("Discord user with ID {UserId} not found in guild {GuildId}.", userId, guild.Id);
                return null;
            }

            return user;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Discord user with ID {UserId}.", userId);
            return null;
        }
    }
}
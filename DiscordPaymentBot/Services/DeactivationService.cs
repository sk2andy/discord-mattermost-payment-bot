using discord_payment_bot.Models;
using discord_payment_bot.Services.Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace discord_payment_bot.Services;

public class DeactivationService
{
    private readonly DiscordSocketClient _client;
    private readonly DiscordMessageSender _discordMessageSender;
    private readonly ILogger<DeactivationService> _logger;
    private readonly BotOptions _options;

    public DeactivationService(
        DiscordSocketClient client,
        DiscordMessageSender discordMessageSender,
        IOptions<BotOptions> botOptions,
        ILogger<DeactivationService> logger)
    {
        _client = client;
        _discordMessageSender = discordMessageSender;
        _options = botOptions.Value;
        _logger = logger;
    }

    public async Task DeactivateUserAsync(SocketGuildUser user, SocketRole role, DeactivationType action)
    {
        switch (action)
        {
            case DeactivationType.RemoveRole:
                // Rolle vom Benutzer entfernen
                await user.RemoveRoleAsync(role);
                _logger.LogInformation("Removed role '{Role}' from user {UserId}.", role.Name, user.Id);
                break;
            case DeactivationType.Kick:
                // Benutzer vom Server kicken
                await user.KickAsync("Trial period expired");
                _logger.LogInformation("Kicked {UserId} from server.", user.Id);
                break;
            case DeactivationType.Ban:
                // Benutzer bannen (mit 0 Tagen Nachrichtenlöschung)
                await user.Guild.AddBanAsync(user, 0, "Trial period expired");
                _logger.LogInformation("Banned {UserId} from server.", user.Id);
                break;
            case DeactivationType.NotifyMods:
                await _discordMessageSender.SendModChannelMessageAsync(
                    $"User {user.Username} has reached the end of their trial period. Please take appropriate action.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }
}
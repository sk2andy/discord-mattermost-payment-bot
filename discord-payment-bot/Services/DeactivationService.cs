using discord_payment_bot.Models;
using Discord.WebSocket;

namespace discord_payment_bot.Services;

public class DeactivationService
{
    private readonly ILogger<DeactivationService> _logger;

    public DeactivationService(ILogger<DeactivationService> logger)
    {
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
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }
}
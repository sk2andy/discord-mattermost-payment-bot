using Discord.Net;
using Discord.WebSocket;

namespace discord_payment_bot.Services;

public class ReminderService
{
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(ILogger<ReminderService> logger)
    {
        _logger = logger;
    }

    public async Task SendReminderAsync(SocketGuildUser user, string paymentLink)
    {
        try
        {
            var dmChannel = await user.CreateDMChannelAsync();
            var message = $"Dear {user.Username}, " + 
                          "your test phase is nearly over. Please use the following link to do the payment and avoid being kicked from the server: " +
                          paymentLink;
            await dmChannel.SendMessageAsync(message);
        }
        catch (HttpException ex)
        {
            if (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Could not send DM to {Username} (DMs turned off?).", user.Username);
            }
            else
            {
                _logger.LogError(ex, "Error sending DM to {Username}.", user.Username);
            }
        }
    }
}
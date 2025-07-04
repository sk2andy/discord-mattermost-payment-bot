namespace discord_payment_bot.Models;

public class ServerMember : ITimestamped
{
    public ulong UserId { get; set; }
    public string? MattermostId { get; set; }
    public string? Email { get; set; }
    public string? DiscordUsername { get; set; }
    public string? MattermostUsername { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
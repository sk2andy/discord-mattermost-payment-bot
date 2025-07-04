namespace discord_payment_bot.Models;

public interface ITimestamped
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
namespace discord_payment_bot.Models;

public class BotOptions
{
    public ulong GuildId { get; set; }
    public string RoleName { get; set; }
    public string BotToken { get; set; }
    public string PaymentLink { get; set; }
    public string PayedRole { get; set; }
    public decimal PaymentAmount { get; set; }
    public int[] ReminderDays { get; set; }
    public int DeactivationDays { get; set; }
    public DeactivationType DeactivationType { get; set; }
    
    public ulong ModChannelId { get; set; }
}
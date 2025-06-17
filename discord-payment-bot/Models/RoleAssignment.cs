namespace discord_payment_bot.Models;

public class RoleAssignment
{
    public Guid Id { get; set; }
    public ulong UserId { get; set; }
    public DateTimeOffset AssignedDate { get; set; }
    public string[] RoleNames  { get; set; }
    public DateTimeOffset? LastReminderDaySent { get; set; }
    public string? DeactivationAction { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
}
namespace discord_payment_bot.Models.Wise;

public class Transaction  : ITimestamped
{
    public Guid Id { get; set; }
    public ulong UserId { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? PaymentReference { get; set; }
    public DateTimeOffset PaymentDate { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? SenderAccount { get; set; }
    public string? TransactionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
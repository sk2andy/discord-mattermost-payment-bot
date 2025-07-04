using discord_payment_bot.Models.Wise;

namespace discord_payment_bot.Services;

public interface IPaymentApi
{
    Task<List<Transaction>> GetIncomeTransactionsAsync(DateTimeOffset since, decimal minAmount,
        string requestCurrency = "USD", CancellationToken cancellationToken = default);

    Task<string> SendMoneyBackAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
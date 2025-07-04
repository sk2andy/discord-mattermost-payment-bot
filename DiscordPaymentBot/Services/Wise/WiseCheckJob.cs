using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using discord_payment_bot.Models;
using discord_payment_bot.Models.Wise;
using discord_payment_bot.Services.Discord;
using discord_payment_bot.Services.Mattermost;
using discord_payment_bot.Services.Orchestration;
using Microsoft.Extensions.Options;
using Quartz;

namespace discord_payment_bot.Services.Wise;

public class WiseCheckJob : IJob
{
    private readonly WiseApi _paymentApi;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<WiseCheckJob> _logger;
    private readonly RegistrationOrchestrator _registrationOrchestrator;
    private readonly RefundService _refundService;
    private readonly IOptions<BotOptions> _botOptions;
    private readonly DiscordMessageSender _discordMessageSender;
    private static readonly Regex UserIdRegex = new(@"\b(\d{17,20})\b", RegexOptions.Compiled);
    const string DeviceFingerPrintSettingsKey = "WiseDeviceFingerprint";

    public WiseCheckJob(
        IPaymentApi paymentApi,
        AppDbContext dbContext,
        ILogger<WiseCheckJob> logger,
        RegistrationOrchestrator registrationOrchestrator,
        RefundService refundService,
        IOptions<BotOptions> botOptions,
        DiscordMessageSender discordMessageSender
        )
    {
        _paymentApi = paymentApi as WiseApi 
            ?? throw new ArgumentException("IPaymentApi must be of type WiseApi", nameof(paymentApi));;
        _dbContext = dbContext;
        _logger = logger;
        _registrationOrchestrator = registrationOrchestrator;
        _refundService = refundService;
        _botOptions = botOptions;
        _discordMessageSender = discordMessageSender;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await RegisterDeviceFingerprintAsync(context.CancellationToken);

        var since = DateTime.UtcNow.AddDays(-7);
        var transactions = await _paymentApi.GetIncomeTransactionsAsync(since, 0, cancellationToken: context.CancellationToken);
        foreach (var tx in transactions)
        {
            var userId = ExtractUserId(tx.Reference) ?? ExtractUserId(tx.PaymentReference);
            if (!userId.HasValue)
            {
                await HandleNotFoundUserIdAsync(tx, context.CancellationToken);
                continue;
            }
            
            if (_dbContext.WiseTransactions.Any(t => t.Reference == tx.Reference && t.PaymentDate == tx.PaymentDate))
                continue;
            if (!string.Equals(tx.Currency, "USD", StringComparison.OrdinalIgnoreCase))
            {
                await RefundWrongCurrencyPaymentAsync(tx, userId, context.CancellationToken);
                continue;
            }
            tx.UserId = userId.Value;
            tx.Id = Guid.NewGuid();
            await _dbContext.WiseTransactions.AddAsync(tx, context.CancellationToken);
            _logger.LogInformation("Saved new Wise transaction for {UserId}: {Amount} at {PaymentDate}", userId, tx.Amount, tx.PaymentDate);

            if (tx.Amount >= _botOptions.Value.PaymentAmount)
            {
                await ProcessCompletePaymentAsync(userId, tx, context.CancellationToken);
            }
            else
            {
                await ProcessIncompletePaymentAsync(userId, tx, context.CancellationToken);
            }
        }
        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }

    private async Task RegisterDeviceFingerprintAsync(CancellationToken cancellationToken = default)
    {
        var fingerprint = await _dbContext.ApplicationSettings.FindAsync([DeviceFingerPrintSettingsKey], cancellationToken: cancellationToken);
        if (fingerprint is null)
        {
            var fingerprintKey = await _paymentApi.CreateDeviceFingerprintAsync(cancellationToken);
            await _dbContext.ApplicationSettings.AddAsync(new ApplicationSetting
            {
                Key = DeviceFingerPrintSettingsKey,
                Value = fingerprintKey
            }, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task HandleNotFoundUserIdAsync(Transaction tx, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Could not find user with id: {PaymentReference}", tx.PaymentReference);
        await _discordMessageSender.SendModChannelMessageAsync(
            $"Could not find user ID in Wise transaction reference: {tx.Reference} or payment reference: {tx.PaymentReference}. Transaction details: {tx.Amount} USD on {tx.PaymentDate:O}", cancellationToken);
    }

    private async Task RefundWrongCurrencyPaymentAsync(Transaction tx, [DisallowNull] ulong? userId, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning($"Wrong currency {tx.Currency} for transaction {tx.Reference}, starting refund...");
        await _discordMessageSender.SendDirectMessageAsync(userId.Value,
            $"Your Wise transaction was in the wrong currency ({tx.Currency}). We are processing a refund. Please only use USD", CancellationToken.None);
        await _refundService.ProcessRefundAsync(tx, cancellationToken);
    }

    private async Task ProcessIncompletePaymentAsync([DisallowNull] ulong? userId, Transaction tx, CancellationToken cancellationToken = default)
    {
        var twoMonthsAgo = DateTime.UtcNow.AddMonths(-2);
        var userPayments = _dbContext.WiseTransactions
            .Where(t => t.UserId == userId.Value && t.PaymentDate >= twoMonthsAgo)
            .ToList();

        var totalPaid = userPayments.Sum(t => t.Amount);
        if (totalPaid + tx.Amount >= _botOptions.Value.PaymentAmount)
        {
            _logger.LogInformation("User {UserId} has completed the payment with total amount: {PayedAmount} USD", userId, totalPaid + tx.Amount);
            await ProcessCompletePaymentAsync(userId, tx, cancellationToken);
        }
        else
        {
            var amountLeft = _botOptions.Value.PaymentAmount - (totalPaid + tx.Amount);
            _logger.LogInformation("User {UserId} still needs to pay {AmountLeft} USD to complete the payment", userId, amountLeft);
            await _discordMessageSender.SendDirectMessageAsync(userId.Value, $"Your Wise transaction of {tx.Amount} USD has been received, but you still need to pay {amountLeft} USD to complete your payment. Please ensure you pay the full amount to proceed with the Mattermost user creation.", CancellationToken.None);
        }
    }

    private async Task ProcessCompletePaymentAsync([DisallowNull] ulong? userId, Transaction tx, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transaction matches payment amount for user {UserId}: {Amount} USD", userId, tx.Amount);
        await _discordMessageSender.SendDirectMessageAsync(userId.Value,
            $"Your Wise transaction of {tx.Amount} USD has been confirmed. We are starting the Mattermost user creation process.", cancellationToken);
        _logger.LogInformation("Starting Mattermost user creation process for Discord user ID {DiscordUserId}", userId.Value);

        await _registrationOrchestrator.StartRegisterUserAsync(tx, userId.Value, cancellationToken);
        
    }

    private static ulong? ExtractUserId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = UserIdRegex.Match(text);
        if (match.Success && ulong.TryParse(match.Groups[1].Value, out var userId))
            return userId;
        return null;
    }
}
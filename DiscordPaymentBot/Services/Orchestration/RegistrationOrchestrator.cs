using discord_payment_bot.Models.Wise;
using discord_payment_bot.Services.Discord;
using discord_payment_bot.Services.Mattermost;
using ServerMember = discord_payment_bot.Models.ServerMember;

namespace discord_payment_bot.Services.Orchestration;

public class RegistrationOrchestrator
{
    private readonly DiscordGetUserService _discordGetUserService;
    private readonly DiscordMessageSender _discordMessageSender;
    private readonly CreateMattermostUserService _createMattermostUserService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<RegistrationOrchestrator> _logger;

    public RegistrationOrchestrator(
        DiscordGetUserService discordGetUserService,
        DiscordMessageSender discordMessageSender,
        CreateMattermostUserService createMattermostUserService,
        AppDbContext dbContext,
        ILogger<RegistrationOrchestrator> logger)
    {
        _discordGetUserService = discordGetUserService;
        _discordMessageSender = discordMessageSender;
        _createMattermostUserService = createMattermostUserService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task StartRegisterUserAsync(Transaction tx, ulong userId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get Discord user
            if(await _discordGetUserService.GetUserAsync(userId, cancellationToken) is not { } discordUser)
            {
                await _discordMessageSender.SendModChannelMessageAsync(
                    $"Received payment for Discord user {userId} that was no found. Payment sender was {tx.SenderAccount} and Reference \"{tx.PaymentReference} {tx.Reference}\" and Amount {tx.Amount} {tx.Currency}. Date: {tx.PaymentDate:O}", cancellationToken);
                return;
            }

            var dbUser = await _dbContext.ServerMembers.FindAsync([userId], cancellationToken: cancellationToken);

            if (dbUser is null or { MattermostId: null })
            {
                await _discordMessageSender.SendDirectMessageAsync(userId, "We received your payment, to register yourself on Mattermost please type in \"/register <Mattermost-email> <Mattermost-username>\" in the Discord channel 'payment'(dc link)", cancellationToken); // ToDo add dc link
            }
            else
            {
                await _discordMessageSender.SendDirectMessageAsync(userId,
                    "We received your payment and extended your membership for another year. Thank you for being a valuable member!", cancellationToken);
            }

            if (dbUser is null)
            {
                await _dbContext.ServerMembers.AddAsync(new ServerMember()
                {
                    UserId = userId,
                    DiscordUsername = discordUser.Username,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while registering user with ID {UserId}.", userId);
        }
    }

    public async Task FinishRegisterUserAsync(ulong userId, string email, string username,
        string password, CancellationToken cancellationToken = default)
    {
        var (created, mattermostUser) =await _createMattermostUserService.TryCreateUserAsync(username, email, password, cancellationToken);
        if (!created)
        {
            _logger.LogError("Failed to create Mattermost user for {Username} ({Email}). User already exists or an error occurred.", username, email);
            await _discordMessageSender.SendDirectMessageAsync(userId, "Failed to create your Mattermost user. Please try again or ask an admin after multiple tries.", cancellationToken);
            return;
        }

        if (mattermostUser is null)
        {
            _logger.LogWarning("Mattermost user creation returned null for {Username} ({Email}).", username, email);
        }
        
        var dbUser = await _dbContext.ServerMembers.FindAsync([userId], cancellationToken: cancellationToken);
        if (dbUser is null)
        {
            _logger.LogWarning("No server member found for user ID {UserId} during registration completion.", userId);
            dbUser = new ServerMember
            {
                UserId = userId,
                DiscordUsername = (await _discordGetUserService.GetUserAsync(userId, cancellationToken))?.Username,
            };
            await _dbContext.ServerMembers.AddAsync(dbUser, cancellationToken);
        }
        
        dbUser.Email = email;
        dbUser.MattermostUsername = username;
        dbUser.MattermostId = mattermostUser?.Id;
        
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Successfully registered user {UserId} with Mattermost ID {MattermostId}.", userId, mattermostUser?.Id);
        await _discordMessageSender.SendDirectMessageAsync(userId, 
            "Your registration is complete! You can now access Mattermost with your new account here: mattermost url.", cancellationToken);// ToDo add mattermost url
    }
}
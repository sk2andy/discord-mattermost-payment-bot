using discord_payment_bot.Models;
using Mattermost.Client.Api.V4.Users;
using Mattermost.Client.Models;
using Microsoft.Extensions.Options;

namespace discord_payment_bot.Services.Mattermost;

public class CreateMattermostUserService : MattermostService
{
    private readonly ILogger<CreateMattermostUserService> _logger;

    public CreateMattermostUserService(
        IOptions<MattermostOptions> options,
        MattermostAccessTokenProvider accessTokenProvider,
        ILogger<CreateMattermostUserService> logger) : base(options, accessTokenProvider)
    {
        _logger = logger;
    }
    
    public async Task<(bool success, User? user)> TryCreateUserAsync(string? username,
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(username);
            ArgumentNullException.ThrowIfNull(email);
            ArgumentNullException.ThrowIfNull(password);

            _logger.LogInformation("Creating Mattermost user with username {Username} and email {Email}", username, email);

            var user = await Client.Api.V4.Users.Email[email].GetAsync(cancellationToken: cancellationToken);
            if (user is not null)
            {
                _logger.LogError("User with email {Email}  already exists on Mattermost", email);
                return (false, user);
            }

            user = await Client.Api.V4.Users.PostAsync(new UsersPostRequestBody
            {
                Email = email,
                Username = username,
                Password = password
            }, cancellationToken: cancellationToken);

            if (user is null)
            {
                _logger.LogError("User with email {Email} could not be created", email);
                return (false, null);
            }

            _logger.LogInformation("User created successfully with username {Username}", username);
            return (true, user);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create user {Email} on Mattermost", email);
            return (false, null);
        }
    }
}
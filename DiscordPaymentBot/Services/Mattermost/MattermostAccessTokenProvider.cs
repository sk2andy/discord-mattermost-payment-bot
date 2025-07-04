using discord_payment_bot.Models;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;

namespace discord_payment_bot.Services.Mattermost;

public class MattermostAccessTokenProvider: IAccessTokenProvider
{
    private readonly string _token;

    public MattermostAccessTokenProvider(IOptions<MattermostOptions> options)
    {
        _token = options.Value.Token;
    }
    
    public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object>? additionalAuthenticationContext = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        return Task.FromResult(_token);
    }

    public AllowedHostsValidator AllowedHostsValidator { get; }
}
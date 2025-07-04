using discord_payment_bot.Models;
using Mattermost.Client;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace discord_payment_bot.Services.Mattermost;

public abstract class MattermostService
{
    protected readonly MattermostOptions Options;
    protected MattermostClient Client { get; set; }

    public MattermostService(IOptions<MattermostOptions> options, MattermostAccessTokenProvider accessTokenProvider)
    {
        Options = options.Value;
        var authProvider = new BaseBearerTokenAuthenticationProvider(accessTokenProvider);
        var adapter = new HttpClientRequestAdapter(authProvider);
        Client = new MattermostClient(adapter);
    }


    
}
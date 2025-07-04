using discord_payment_bot.Models.Wise;
using discord_payment_bot.Services;
using discord_payment_bot.Services.Wise;

namespace discord_payment_bot.Extensions.Wise;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWiseApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<WiseOptions>(configuration.GetSection(nameof(WiseOptions)));
        services.AddHttpClient<IPaymentApi, WiseApi>(httpClient =>
        {
            var baseUrl = configuration.GetValue<string>("WiseOptions:ApiBaseUrl") 
                          ?? throw new ArgumentNullException("ApiBaseUrl");
            
            httpClient.BaseAddress = new Uri(baseUrl);
            var apiKey = configuration.GetValue<string>("WiseOptions:ApiKey")
                         ?? throw new ArgumentNullException("ApiBaseUrl");
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        });
        
        return services;
    }
}
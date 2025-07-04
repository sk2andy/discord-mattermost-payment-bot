using discord_payment_bot.Models;
using Mattermost.Client.Api.V4.Users.Item.Active;
using Microsoft.Extensions.Options;
using Quartz;

namespace discord_payment_bot.Services.Mattermost;

public class EnableDisableMattermostUserService : MattermostService, IJob
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<EnableDisableMattermostUserService> _logger;
    public static readonly JobKey JobKey = JobKey.Create(nameof(EnableDisableMattermostUserService));

    public EnableDisableMattermostUserService(
        IOptions<MattermostOptions> options,
        MattermostAccessTokenProvider accessTokenProvider,
        ISchedulerFactory schedulerFactory,
        ILogger<EnableDisableMattermostUserService> logger) : base(options, accessTokenProvider)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task StartLocProcessProcessAsync(
        string email,
        bool enable,
        CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var job = JobBuilder.Create<EnableDisableMattermostUserService>()
            .WithIdentity(JobKey)
            .UsingJobData("email", email)
            .UsingJobData("enable", enable)
            .Build();
        
        var trigger = TriggerBuilder.Create()
            .WithIdentity(nameof(EnableDisableMattermostUserService) + Guid.NewGuid())
            .StartNow()
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }
    
    public async Task EnableDisableUserAsync(
        string? email,
        bool enable,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(email);

            _logger.LogInformation("Disabling or enabling Mattermost user with email {Email}. Enabling: {Enable}",email, enable);

            var user = await Client.Api.V4.Users.Email[email].GetAsync(cancellationToken: cancellationToken);
            if (user is null)
            {
                _logger.LogError("User with email {Email} does not exists on Mattermost", email);
                return;
            }

            await Client.Api.V4.Users[user.Id].Active.PutAsync(new ActivePutRequestBody
            {
                Active = enable
            }, cancellationToken: cancellationToken);

            _logger.LogInformation("Disabled or enabling user successfully with email {Email} and username {Username}. Enabling: {Enable}", email, user.Username, enable);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to disable or disable user {Email} on Mattermost. Enabling: {Enable}", email, enable);
        }
    }

    public Task Execute(IJobExecutionContext context)
    {
        var email = context.MergedJobDataMap.GetString("email");
        var enable = context.MergedJobDataMap.GetBooleanValue("enable");
        return EnableDisableUserAsync(email, enable, context.CancellationToken);
    }
}
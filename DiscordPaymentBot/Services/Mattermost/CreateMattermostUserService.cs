using discord_payment_bot.Models;
using Mattermost.Client.Api.V4.Users;
using Microsoft.Extensions.Options;
using Quartz;

namespace discord_payment_bot.Services.Mattermost;

public class CreateMattermostUserService : MattermostService, IJob
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<CreateMattermostUserService> _logger;
    public static readonly JobKey JobKey = JobKey.Create(nameof(CreateMattermostUserService));

    public CreateMattermostUserService(
        IOptions<MattermostOptions> options,
        MattermostAccessTokenProvider accessTokenProvider,
        ISchedulerFactory schedulerFactory,
        ILogger<CreateMattermostUserService> logger) : base(options, accessTokenProvider)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task StartCreationProcessAsync(
        string email,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        var job = JobBuilder.Create<CreateMattermostUserService>()
            .WithIdentity(JobKey)
            .UsingJobData("email", email)
            .UsingJobData("username", username)
            .UsingJobData("password", password)
            .Build();
        
        var trigger = TriggerBuilder.Create()
            .WithIdentity(nameof(CreateMattermostUserService) + Guid.NewGuid())
            .StartNow()
            .Build();

        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }
    
    public async Task CreateUserAsync(
        string? username,
        string? email,
        string? password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(username);
            ArgumentNullException.ThrowIfNull(email);
            ArgumentNullException.ThrowIfNull(password);

            _logger.LogInformation("Creating Mattermost user with username {Username} and email {Email}",
                username, email);

            var user = await Client.Api.V4.Users.Email[email].GetAsync(cancellationToken: cancellationToken);
            if (user is not null)
            {
                _logger.LogError("User with email {Email}  already exists on Mattermost", email);
                return;
            }

            await Client.Api.V4.Users.PostAsync(new UsersPostRequestBody
            {
                Email = email,
                Username = username,
                Password = password
            }, cancellationToken: cancellationToken);

            _logger.LogInformation("User created successfully with username {Username}", username);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create user {Email} on Mattermost", email);
        }
    }

    public Task Execute(IJobExecutionContext context)
    {
        var email = context.MergedJobDataMap.GetString("email");
        var username = context.MergedJobDataMap.GetString("username");
        var password = context.MergedJobDataMap.GetString("password");
        
        return CreateUserAsync(
            email,
            username,
            password,
            context.CancellationToken);
    }
}
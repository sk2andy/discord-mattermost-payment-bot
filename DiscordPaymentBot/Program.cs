using discord_payment_bot.Extensions.Wise;
using discord_payment_bot.Models;
using discord_payment_bot.Services;
using discord_payment_bot.Services.Discord;
using discord_payment_bot.Services.Mattermost;
using discord_payment_bot.Services.Orchestration;
using discord_payment_bot.Services.Wise;
using Discord;
using Discord.WebSocket;
using Quartz;

var builder = WebApplication.CreateBuilder(args);


builder.Services.Configure<BotOptions>(builder.Configuration.GetSection(nameof(BotOptions)));
builder.Services.Configure<MattermostOptions>(builder.Configuration.GetSection(nameof(MattermostOptions)));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(nameof(DatabaseOptions)));

builder.Services.AddLogging(logging => {
    logging.AddConsole();
});

builder.Services.AddDbContext<AppDbContext>();

var client = new DiscordSocketClient(new DiscordSocketConfig {
    GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages,
    AlwaysDownloadUsers = true
});
builder.Services.AddSingleton(client);
builder.Services.AddScoped<ReminderService>();
builder.Services.AddScoped<DeactivationService>();
builder.Services.AddScoped<DiscordMessageSender>();
builder.Services.AddScoped<DiscordGetUserService>();
builder.Services.AddSingleton<CreateMattermostUserService>();
builder.Services.AddSingleton<RefundService>();
builder.Services.AddSingleton<MattermostAccessTokenProvider>();
builder.Services.AddSingleton<CreateMattermostUserService>();
builder.Services.AddSingleton<EnableDisableMattermostUserService>();
builder.Services.AddScoped<RegistrationOrchestrator>();

builder.Services.AddHostedService<BotService>();
builder.Services.AddWiseApi(builder.Configuration);

builder.Services.AddQuartz(q =>
{
    var roleCheckJobKey = new JobKey("RoleCheckJob");
    //q.AddJob<RoleCheckJob>(opts => opts.WithIdentity(roleCheckJobKey));
    
    // q.AddTrigger(opts => opts
    //     .ForJob(roleCheckJobKey)
    //     .WithIdentity("RoleCheckTrigger")
    //     .WithCronSchedule("0 * * * * ?"));
    
    var wiseCheckJobKey = new JobKey("WiseCheckJob");
    q.AddJob<WiseCheckJob>(opts => opts.WithIdentity(wiseCheckJobKey));
    q.AddTrigger(opts => opts
        .ForJob(wiseCheckJobKey)
        .WithIdentity("WiseCheckTrigger")
        .WithCronSchedule("0 * * * * ?"));

    // var createMattermostUserJobKey = CreateMattermostUserService.JobKey;
    // q.AddJob<CreateMattermostUserService>(opts => opts.WithIdentity(createMattermostUserJobKey));
    // q.AddTrigger(opts =>
    //         opts.ForJob(createMattermostUserJobKey));
    // var enableDisableMattermostUserJobKey = CreateMattermostUserService.JobKey;
    // q.AddJob<EnableDisableMattermostUserService>(opts => opts.WithIdentity(enableDisableMattermostUserJobKey));
    // q.AddTrigger(opts =>
    //     opts.ForJob(enableDisableMattermostUserJobKey));
});
builder.Services.AddQuartzHostedService(options => {
    options.WaitForJobsToComplete = true;
});

var app = builder.Build();

await app.RunAsync();
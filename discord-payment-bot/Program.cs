using discord_payment_bot.Models;
using discord_payment_bot.Services;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("BotOptions"));

builder.Services.AddLogging(logging => {
    logging.AddConsole();
});

var connString = builder.Configuration.GetConnectionString("BotDatabase") 
                 ?? "Data Source=botdata.db";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connString));

builder.Services.AddSingleton<DiscordSocketClient>();
builder.Services.AddSingleton<ReminderService>();
builder.Services.AddSingleton<DeactivationService>();
builder.Services.AddHostedService<BotService>();

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("RoleCheckJob");
    q.AddJob<RoleCheckJob>(opts => opts.WithIdentity(jobKey));
    
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("RoleCheckTrigger")
        .WithCronSchedule("0 0 1 * * ?"));
});
builder.Services.AddQuartzHostedService(options => {
    options.WaitForJobsToComplete = true;
});

var app = builder.Build();

await app.RunAsync();
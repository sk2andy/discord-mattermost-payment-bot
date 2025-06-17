using discord_payment_bot.Models;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using Quartz.Xml.JobSchedulingData20;

namespace discord_payment_bot.Services;

public class RoleCheckJob : Quartz.IJob
{
    private readonly AppDbContext _db;
    private readonly ReminderService _reminderService;
    private readonly DeactivationService _deactivationService;
    private readonly BotOptions _config;
    private readonly DiscordSocketClient _discord;
    private readonly ILogger<RoleCheckJob> _logger;

    public RoleCheckJob(AppDbContext db, ReminderService reminderService, 
        DeactivationService deactivationService,
        IOptions<BotOptions> config, DiscordSocketClient discord,
        ILogger<RoleCheckJob> logger)
    {
        _db = db;
        _reminderService = reminderService;
        _deactivationService = deactivationService;
        _config = config.Value;
        _discord = discord;
        _logger = logger;
    }

    public async Task Execute(Quartz.IJobExecutionContext context)
    {
        _logger.LogInformation("Start checking roles for guild {GuildId} at {DateTime}", 
            _config.GuildId, DateTimeOffset.Now);

        var guilds = _discord.Guilds;
        var guild = _discord.GetGuild(_config.GuildId);
        if (guild == null)
        {
            _logger.LogError("Guild with ID {GuildId} not found.", _config.GuildId);
            return;
        }
        var role = guild.Roles.FirstOrDefault(r => r.Name == _config.RoleName);
        if (role == null)
        {
            _logger.LogError("Role '{Role}' not found on server.", _config.RoleName);
            return;
        }
        
        var usersWithRole = guild.Users.Where(u => u.Roles.Any(r => r.Id == role.Id)).ToList();

        var trackedIds = usersWithRole.Select(u => u.Id).ToHashSet();
        var obsoleteEntries = _db.RoleAssignments
            .Where(entry => !trackedIds.Contains(entry.UserId))
            .ToList();
        if (obsoleteEntries.Count != 0)
        {
            _db.RoleAssignments.RemoveRange(obsoleteEntries);
            _logger.LogInformation("Remove {ObsoleteEntriesCount} old entries (has not the role assignment {Role} anymore).", 
                obsoleteEntries.Count, _config.RoleName);
        }
        
        foreach (var user in usersWithRole)
        {
            var entry = _db.RoleAssignments.SingleOrDefault(e => e.UserId == user.Id);
            if (entry == null)
            {
                entry = new RoleAssignment 
                { 
                    Id = Guid.NewGuid(), 
                    UserId = user.Id, 
                    AssignedDate = DateTime.UtcNow, 
                    LastReminderDaySent = DateTimeOffset.MinValue,
                    RoleNames = user.Roles.Select(r => r.Name).ToArray()
                };
                _db.RoleAssignments.Add(entry);
                _logger.LogInformation("New user in role {Role}: UserId={UserId}",_config.RoleName,  user.Id);
            }
            
            if(entry.DeactivationAction is not null) continue;
            
            var daysInRole = (DateTime.UtcNow - entry.AssignedDate).TotalDays;

            foreach (var remindAfter in _config.ReminderDays.OrderBy(d => d))
            {
                var scheduledDate = entry.AssignedDate.AddDays(remindAfter);
                if (DateTimeOffset.UtcNow < scheduledDate || entry.LastReminderDaySent >= scheduledDate) continue;

                await _reminderService.SendReminderAsync(user, _config.PaymentLink);
                entry.LastReminderDaySent = DateTimeOffset.UtcNow;
                _logger.LogInformation("Reminder after {Days} days sent to user {User}.", remindAfter, user.Id);
            }

            if (daysInRole < _config.DeactivationDays) continue;
            _logger.LogInformation("User {User} is longer than {Days} days in role {Role}. Action: {Action}",
                user.Id, _config.DeactivationDays, _config.RoleName , _config.DeactivationType);
            await _deactivationService.DeactivateUserAsync(user, role, _config.DeactivationType);

            entry.DeactivationAction = _config.DeactivationType.ToString();
            entry.DeactivatedAt = DateTimeOffset.UtcNow;
        }
        
        await _db.SaveChangesAsync();
        _logger.LogInformation("Finished checking roles for guild {GuildId} at {DateTimeOffset}",
            _config.GuildId, DateTimeOffset.Now);
        }
}
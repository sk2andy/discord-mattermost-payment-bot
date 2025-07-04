using discord_payment_bot.Models;
using discord_payment_bot.Models.Converters;
using discord_payment_bot.Models.Wise;
using Microsoft.EntityFrameworkCore;

namespace discord_payment_bot.Services;

public class AppDbContext : DbContext
{
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<Transaction> WiseTransactions => Set<Transaction>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();

    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleAssignment>()
            .HasKey(ra => ra.Id);

        modelBuilder.Entity<RoleAssignment>()
            .Property(ra => ra.UserId)
            .IsRequired();

        modelBuilder.Entity<RoleAssignment>()
            .Property(x => x.RoleNames)
            .HasConversion(new JsonArrayConverter<string>());
        
        modelBuilder.Entity<Transaction>()
            .HasKey(ra => ra.Id);

        modelBuilder.Entity<Transaction>()
            .Property(ra => ra.UserId)
            .IsRequired();
        
        modelBuilder.Entity<ApplicationSetting>()
            .HasKey(ra => ra.Key);
    }
    
}
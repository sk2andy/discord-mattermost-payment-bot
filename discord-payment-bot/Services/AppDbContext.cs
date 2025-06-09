using discord_payment_bot.Models;
using discord_payment_bot.Models.Converters;
using Microsoft.EntityFrameworkCore;

namespace discord_payment_bot.Services;

public class AppDbContext : DbContext
{
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

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
    }
    
}
using discord_payment_bot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

public class TimestampSaveChangesInterceptor : SaveChangesInterceptor
{
    private static DateTime UtcNow => DateTime.UtcNow;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is null) 
            return base.SavingChanges(eventData, result);

        foreach (var entry in eventData.Context.ChangeTracker
                     .Entries<ITimestamped>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = UtcNow;
                entry.Entity.UpdatedAt = UtcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = UtcNow;
            }
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            foreach (var entry in eventData.Context.ChangeTracker
                         .Entries<ITimestamped>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = UtcNow;
                    entry.Entity.UpdatedAt = UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = UtcNow;
                }
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
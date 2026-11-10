using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SeatCharter.Api.BusinessLogic.Interfaces;
using SeatCharter.Api.Models;

namespace SeatCharter.Api.BusinessLogic.Services;

/// <summary>
/// Collects ids of changed users while saving and evicts them from the user cache only after the commit,
/// so role/active changes take effect on the next request without anyone remembering to invalidate.
/// </summary>
public class UserCacheInvalidationInterceptor : SaveChangesInterceptor
{
    private readonly IUserCacheService _cache;
    // Singleton interceptor, many contexts: keep the pending set per context.
    private readonly ConditionalWeakTable<DbContext, HashSet<string>> _pending = new();

    public UserCacheInvalidationInterceptor(IUserCacheService cache) => _cache = cache;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Flush(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null) _pending.Remove(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) _pending.Remove(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        var ids = context.ChangeTracker.Entries<User>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity.Id)
            .ToList();
        if (ids.Count == 0) return;
        _pending.GetOrCreateValue(context).UnionWith(ids);
    }

    private void Flush(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var ids)) return;
        foreach (var id in ids) _cache.Invalidate(id);
        _pending.Remove(context);
    }
}

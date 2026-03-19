using Microsoft.EntityFrameworkCore;
using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;
using SupportAssignmentSystem.Infrastructure.Data;

namespace SupportAssignmentSystem.Infrastructure.Storage;

/// <summary>
/// Database storage implementation using Entity Framework Core
/// Persistent storage, shared across processes, but slower than Redis
/// </summary>
public class DatabaseSessionStorage : ISessionStorage
{
    private readonly IDbContextFactory<SupportAssignmentDbContext> _contextFactory;

    public DatabaseSessionStorage(IDbContextFactory<SupportAssignmentDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var entity = await context.ChatSessions.FindAsync(sessionId);
        return entity?.ToEntity();
    }

    public async Task<bool> SaveSessionAsync(ChatSession session)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = ChatSessionEntity.FromEntity(session);
        await context.ChatSessions.AddAsync(entity);
        
        var saved = await context.SaveChangesAsync();
        return saved > 0;
    }

    public async Task<bool> UpdateSessionAsync(ChatSession session)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = ChatSessionEntity.FromEntity(session);
        context.ChatSessions.Update(entity);
        
        var updated = await context.SaveChangesAsync();
        return updated > 0;
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var entity = await context.ChatSessions.FindAsync(sessionId);
        if (entity == null)
            return false;

        context.ChatSessions.Remove(entity);
        var deleted = await context.SaveChangesAsync();
        return deleted > 0;
    }

    public async Task<List<ChatSession>> GetAllSessionsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var entities = await context.ChatSessions.ToListAsync();
        return entities.Select(e => e.ToEntity()).ToList();
    }

    public async Task<List<ChatSession>> GetQueuedSessionsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        var entities = await context.ChatSessions
            .Where(s => s.Status == ChatSessionStatus.Queued ||
                       s.Status == ChatSessionStatus.Assigned ||
                       s.Status == ChatSessionStatus.Active)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.ToEntity()).ToList();
    }

    public async Task<int> GetQueueSizeAsync(bool isOverflow)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        
        return await context.ChatSessions
            .Where(s => s.IsOverflow == isOverflow &&
                       (s.Status == ChatSessionStatus.Queued ||
                        s.Status == ChatSessionStatus.Assigned ||
                        s.Status == ChatSessionStatus.Active))
            .CountAsync();
    }
}

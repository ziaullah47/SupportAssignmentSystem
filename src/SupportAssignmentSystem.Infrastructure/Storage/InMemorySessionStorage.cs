using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;
using System.Collections.Concurrent;

namespace SupportAssignmentSystem.Infrastructure.Storage;

/// <summary>
/// In-memory storage implementation using ConcurrentDictionary
/// Fast but state is lost on restart and not shared across processes
/// </summary>
public class InMemorySessionStorage : ISessionStorage
{
    private readonly ConcurrentDictionary<string, ChatSession> _sessions = new();

    public Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task<bool> SaveSessionAsync(ChatSession session)
    {
        return Task.FromResult(_sessions.TryAdd(session.Id, session));
    }

    public Task<bool> UpdateSessionAsync(ChatSession session)
    {
        _sessions[session.Id] = session;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteSessionAsync(string sessionId)
    {
        return Task.FromResult(_sessions.TryRemove(sessionId, out _));
    }

    public Task<List<ChatSession>> GetAllSessionsAsync()
    {
        return Task.FromResult(_sessions.Values.ToList());
    }

    public Task<List<ChatSession>> GetQueuedSessionsAsync()
    {
        var sessions = _sessions.Values
            .Where(s => s.Status == ChatSessionStatus.Queued ||
                       s.Status == ChatSessionStatus.Assigned ||
                       s.Status == ChatSessionStatus.Active)
            .OrderBy(s => s.CreatedAt)
            .ToList();

        return Task.FromResult(sessions);
    }

    public Task<int> GetQueueSizeAsync(bool isOverflow)
    {
        var count = _sessions.Values.Count(s =>
            s.IsOverflow == isOverflow &&
            (s.Status == ChatSessionStatus.Queued ||
             s.Status == ChatSessionStatus.Assigned ||
             s.Status == ChatSessionStatus.Active));

        return Task.FromResult(count);
    }
}

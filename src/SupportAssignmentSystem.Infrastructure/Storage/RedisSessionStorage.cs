using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace SupportAssignmentSystem.Infrastructure.Storage;

/// <summary>
/// Redis storage implementation
/// Shared across multiple processes, fast, but requires Redis server
/// </summary>
public class RedisSessionStorage : ISessionStorage
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly string _keyPrefix;

    public RedisSessionStorage(IConnectionMultiplexer redis, string keyPrefix = "session:")
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _keyPrefix = keyPrefix;
    }

    private string GetKey(string sessionId) => $"{_keyPrefix}{sessionId}";
    private string GetAllSessionsKey() => $"{_keyPrefix}all";

    public async Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        var json = await _database.StringGetAsync(GetKey(sessionId));
        if (json.IsNullOrEmpty)
            return null;

        return JsonSerializer.Deserialize<ChatSession>(json!);
    }

    public async Task<bool> SaveSessionAsync(ChatSession session)
    {
        var json = JsonSerializer.Serialize(session);
        var saved = await _database.StringSetAsync(GetKey(session.Id), json);

        if (saved)
        {
            // Add to set of all sessions
            await _database.SetAddAsync(GetAllSessionsKey(), session.Id);
        }

        return saved;
    }

    public async Task<bool> UpdateSessionAsync(ChatSession session)
    {
        var json = JsonSerializer.Serialize(session);
        await _database.StringSetAsync(GetKey(session.Id), json);
        return true;
    }

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        var deleted = await _database.KeyDeleteAsync(GetKey(sessionId));
        if (deleted)
        {
            await _database.SetRemoveAsync(GetAllSessionsKey(), sessionId);
        }
        return deleted;
    }

    public async Task<List<ChatSession>> GetAllSessionsAsync()
    {
        var sessionIds = await _database.SetMembersAsync(GetAllSessionsKey());
        var sessions = new List<ChatSession>();

        foreach (var sessionId in sessionIds)
        {
            var session = await GetSessionAsync(sessionId.ToString());
            if (session != null)
                sessions.Add(session);
        }

        return sessions;
    }

    public async Task<List<ChatSession>> GetQueuedSessionsAsync()
    {
        var allSessions = await GetAllSessionsAsync();

        return allSessions
            .Where(s => s.Status == ChatSessionStatus.Queued ||
                       s.Status == ChatSessionStatus.Assigned ||
                       s.Status == ChatSessionStatus.Active)
            .OrderBy(s => s.CreatedAt)
            .ToList();
    }

    public async Task<int> GetQueueSizeAsync(bool isOverflow)
    {
        var allSessions = await GetAllSessionsAsync();

        return allSessions.Count(s =>
            s.IsOverflow == isOverflow &&
            (s.Status == ChatSessionStatus.Queued ||
             s.Status == ChatSessionStatus.Assigned ||
             s.Status == ChatSessionStatus.Active));
    }
}

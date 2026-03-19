using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;

namespace SupportAssignmentSystem.Core.Services;

public class ChatQueueService : IChatQueueService
{
    private readonly ISessionStorage _storage;
    private readonly ITeamManagementService _teamManagementService;

    public ChatQueueService(ISessionStorage storage, ITeamManagementService teamManagementService)
    {
        _storage = storage;
        _teamManagementService = teamManagementService;
    }

    public async Task<ChatSession?> EnqueueChatSessionAsync(string userId)
    {
        var currentShift = GetCurrentShift();
        var activeTeam = await _teamManagementService.GetActiveTeamForShiftAsync(currentShift);

        if (activeTeam == null)
            return null;

        var currentQueueSize = await _storage.GetQueueSizeAsync(false);
        var maxQueueLength = activeTeam.GetMaxQueueLength();

        // Check if main queue is full
        if (currentQueueSize >= maxQueueLength)
        {
            // Check if overflow is available (during office hours)
            if (_teamManagementService.IsOfficeHours())
            {
                var overflowTeam = await _teamManagementService.GetOverflowTeamAsync();
                if (overflowTeam != null)
                {
                    // Activate overflow agents if not already active
                    foreach (var agent in overflowTeam.Agents)
                    {
                        agent.IsActive = true;
                    }

                    var overflowQueueSize = await _storage.GetQueueSizeAsync(true);
                    var overflowMaxQueue = overflowTeam.GetMaxQueueLength();

                    if (overflowQueueSize >= overflowMaxQueue)
                    {
                        // Overflow queue is also full, refuse chat
                        return await CreateRefusedSessionAsync(userId);
                    }

                    // Add to overflow queue
                    var overflowSession = CreateSession(userId, true);
                    await _storage.SaveSessionAsync(overflowSession);
                    return overflowSession;
                }
            }

            // No overflow available, refuse chat
            return await CreateRefusedSessionAsync(userId);
        }

        // Add to main queue
        var session = CreateSession(userId, false);
        await _storage.SaveSessionAsync(session);
        return session;
    }

    public async Task<ChatSession?> GetChatSessionAsync(string sessionId)
    {
        return await _storage.GetSessionAsync(sessionId);
    }

    public async Task<bool> PollChatSessionAsync(string sessionId)
    {
        var session = await _storage.GetSessionAsync(sessionId);
        if (session == null || session.Status == ChatSessionStatus.Refused)
            return false;

        session.LastPollTime = DateTime.UtcNow;
        session.MissedPollCount = 0;
        await _storage.UpdateSessionAsync(session);
        return true;
    }

    public async Task<List<ChatSession>> GetQueuedSessionsAsync()
    {
        return await _storage.GetQueuedSessionsAsync();
    }

    public async Task MarkSessionInactiveAsync(string sessionId)
    {
        var session = await _storage.GetSessionAsync(sessionId);
        if (session != null)
        {
            session.Status = ChatSessionStatus.Inactive;
            await _storage.UpdateSessionAsync(session);
        }
    }

    public async Task CompleteSessionAsync(string sessionId)
    {
        var session = await _storage.GetSessionAsync(sessionId);
        if (session != null)
        {
            session.Status = ChatSessionStatus.Completed;
            await _storage.UpdateSessionAsync(session);
        }
    }

    private ChatSession CreateSession(string userId, bool isOverflow)
    {
        return new ChatSession
        {
            UserId = userId,
            Status = ChatSessionStatus.Queued,
            IsOverflow = isOverflow,
            CreatedAt = DateTime.UtcNow,
            LastPollTime = DateTime.UtcNow
        };
    }

    private async Task<ChatSession> CreateRefusedSessionAsync(string userId)
    {
        var session = new ChatSession
        {
            UserId = userId,
            Status = ChatSessionStatus.Refused,
            CreatedAt = DateTime.UtcNow
        };
        await _storage.SaveSessionAsync(session);
        return session;
    }

    private ShiftType GetCurrentShift()
    {
        var hour = DateTime.UtcNow.Hour;
        if (hour >= 0 && hour < 8)
            return ShiftType.Morning;
        if (hour >= 8 && hour < 16)
            return ShiftType.Day;
        return ShiftType.Evening;
    }
}

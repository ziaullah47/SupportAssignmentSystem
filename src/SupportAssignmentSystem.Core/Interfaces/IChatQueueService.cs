using SupportAssignmentSystem.Core.Entities;

namespace SupportAssignmentSystem.Core.Interfaces;

public interface IChatQueueService
{
    Task<ChatSession?> EnqueueChatSessionAsync(string userId);
    Task<ChatSession?> GetChatSessionAsync(string sessionId);
    Task<bool> PollChatSessionAsync(string sessionId);
    Task<List<ChatSession>> GetQueuedSessionsAsync();
    Task MarkSessionInactiveAsync(string sessionId);
    Task CompleteSessionAsync(string sessionId);
}

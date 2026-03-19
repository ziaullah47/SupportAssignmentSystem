using SupportAssignmentSystem.Core.Entities;

namespace SupportAssignmentSystem.Core.Interfaces;

/// <summary>
/// Storage abstraction for chat sessions
/// Can be implemented with InMemory, Redis, or Database
/// </summary>
public interface ISessionStorage
{
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task<bool> SaveSessionAsync(ChatSession session);
    Task<bool> UpdateSessionAsync(ChatSession session);
    Task<bool> DeleteSessionAsync(string sessionId);
    Task<List<ChatSession>> GetAllSessionsAsync();
    Task<List<ChatSession>> GetQueuedSessionsAsync();
    Task<int> GetQueueSizeAsync(bool isOverflow);
}

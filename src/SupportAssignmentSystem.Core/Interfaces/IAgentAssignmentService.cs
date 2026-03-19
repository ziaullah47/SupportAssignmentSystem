using SupportAssignmentSystem.Core.Entities;

namespace SupportAssignmentSystem.Core.Interfaces;

public interface IAgentAssignmentService
{
    Task<bool> AssignChatToAgentAsync(ChatSession session);
    Task<Agent?> GetNextAvailableAgentAsync(string? teamId = null, bool isOverflow = false);
    Task ReleaseChatFromAgentAsync(string sessionId);
}

using SupportAssignmentSystem.Core.Enums;

namespace SupportAssignmentSystem.Core.Entities;

public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public ChatSessionStatus Status { get; set; } = ChatSessionStatus.Queued;
    public string? AssignedAgentId { get; set; }
    public string? AssignedTeamId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedAt { get; set; }
    public DateTime LastPollTime { get; set; } = DateTime.UtcNow;
    public int MissedPollCount { get; set; }
    public bool IsOverflow { get; set; }
}

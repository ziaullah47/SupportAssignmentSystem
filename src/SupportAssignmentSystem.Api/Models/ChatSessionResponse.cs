namespace SupportAssignmentSystem.Api.Models;

/// <summary>
/// Response model containing chat session information
/// </summary>
public class ChatSessionResponse
{
    /// <summary>
    /// The unique identifier of the chat session
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the chat session (Queued, Assigned, Active, Inactive, Completed, Refused)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the agent assigned to this session (null if not yet assigned)
    /// </summary>
    public string? AssignedAgentId { get; set; }

    /// <summary>
    /// The timestamp when the session was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The timestamp when an agent was assigned (null if not yet assigned)
    /// </summary>
    public DateTime? AssignedAt { get; set; }

    /// <summary>
    /// Indicates if this session is in the overflow queue
    /// </summary>
    public bool IsOverflow { get; set; }
}

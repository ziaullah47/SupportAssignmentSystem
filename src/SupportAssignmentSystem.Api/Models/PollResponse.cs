namespace SupportAssignmentSystem.Api.Models;

/// <summary>
/// Response model for chat session polling
/// </summary>
public class PollResponse
{
    /// <summary>
    /// Indicates if the poll was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Current status of the chat session
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the assigned agent (null if not yet assigned)
    /// </summary>
    public string? AssignedAgentId { get; set; }
}

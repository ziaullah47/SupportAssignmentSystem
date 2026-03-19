using System.ComponentModel.DataAnnotations;

namespace SupportAssignmentSystem.Api.Models;

/// <summary>
/// Request model for creating a new chat session
/// </summary>
public class CreateChatSessionRequest
{
    /// <summary>
    /// The unique identifier of the user requesting support
    /// </summary>
    /// <example>user123</example>
    [Required]
    public string UserId { get; set; } = string.Empty;
}

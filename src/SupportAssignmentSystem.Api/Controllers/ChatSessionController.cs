using Microsoft.AspNetCore.Mvc;
using SupportAssignmentSystem.Api.Models;
using SupportAssignmentSystem.Core.Interfaces;

namespace SupportAssignmentSystem.Api.Controllers;

/// <summary>
/// Manages chat sessions including creation, polling, and completion
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatSessionController : ControllerBase
{
    private readonly IChatQueueService _chatQueueService;
    private readonly IAgentAssignmentService _agentAssignmentService;
    private readonly ILogger<ChatSessionController> _logger;

    public ChatSessionController(
        IChatQueueService chatQueueService,
        IAgentAssignmentService agentAssignmentService,
        ILogger<ChatSessionController> logger)
    {
        _chatQueueService = chatQueueService;
        _agentAssignmentService = agentAssignmentService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new chat session and adds it to the queue
    /// </summary>
    /// <param name="request">The user information to create a chat session</param>
    /// <returns>The created chat session with status and assignment information</returns>
    /// <response code="200">Chat session created and queued successfully</response>
    /// <response code="400">Invalid request - UserId is required</response>
    /// <response code="503">Chat session refused - all queues are full</response>
    [HttpPost]
    [ProducesResponseType(typeof(ChatSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ChatSessionResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatSessionResponse>> CreateChatSession([FromBody] CreateChatSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new { error = "UserId is required" });
        }

        _logger.LogInformation("Creating chat session for user {UserId}", request.UserId);

        var session = await _chatQueueService.EnqueueChatSessionAsync(request.UserId);

        if (session == null)
        {
            _logger.LogWarning("Failed to create chat session for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "Failed to create chat session" });
        }

        var response = new ChatSessionResponse
        {
            Id = session.Id,
            Status = session.Status.ToString(),
            AssignedAgentId = session.AssignedAgentId,
            CreatedAt = session.CreatedAt,
            AssignedAt = session.AssignedAt,
            IsOverflow = session.IsOverflow
        };

        if (session.Status == Core.Enums.ChatSessionStatus.Refused)
        {
            _logger.LogWarning("Chat session refused for user {UserId} - queue full", request.UserId);
            return StatusCode(503, response);
        }

        _logger.LogInformation("Chat session {SessionId} created successfully for user {UserId}", 
            session.Id, request.UserId);

        return Ok(response);
    }

    /// <summary>
    /// Retrieves the current status of a chat session
    /// </summary>
    /// <param name="sessionId">The unique identifier of the chat session</param>
    /// <returns>The current chat session information</returns>
    /// <response code="200">Chat session found and returned successfully</response>
    /// <response code="404">Chat session not found</response>
    [HttpGet("{sessionId}")]
    [ProducesResponseType(typeof(ChatSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatSessionResponse>> GetChatSession(string sessionId)
    {
        var session = await _chatQueueService.GetChatSessionAsync(sessionId);

        if (session == null)
        {
            return NotFound(new { error = "Chat session not found" });
        }

        var response = new ChatSessionResponse
        {
            Id = session.Id,
            Status = session.Status.ToString(),
            AssignedAgentId = session.AssignedAgentId,
            CreatedAt = session.CreatedAt,
            AssignedAt = session.AssignedAt,
            IsOverflow = session.IsOverflow
        };

        return Ok(response);
    }

    /// <summary>
    /// Polls a chat session to keep it active (should be called every 1 second)
    /// </summary>
    /// <param name="sessionId">The unique identifier of the chat session</param>
    /// <returns>The current poll status including agent assignment</returns>
    /// <response code="200">Poll successful - session is active</response>
    /// <response code="404">Chat session not found</response>
    /// <remarks>
    /// The client must poll this endpoint every 1 second to keep the session active.
    /// If 3 consecutive polls are missed, the session will be marked as inactive.
    /// </remarks>
    [HttpPost("{sessionId}/poll")]
    [ProducesResponseType(typeof(PollResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PollResponse>> PollChatSession(string sessionId)
    {
        var session = await _chatQueueService.GetChatSessionAsync(sessionId);

        if (session == null)
        {
            return NotFound(new { error = "Chat session not found" });
        }

        var success = await _chatQueueService.PollChatSessionAsync(sessionId);

        var response = new PollResponse
        {
            Success = success,
            Status = session.Status.ToString(),
            AssignedAgentId = session.AssignedAgentId
        };

        _logger.LogDebug("Poll request for session {SessionId}, Status: {Status}", 
            sessionId, session.Status);

        return Ok(response);
    }

    /// <summary>
    /// Completes a chat session and releases the assigned agent
    /// </summary>
    /// <param name="sessionId">The unique identifier of the chat session</param>
    /// <returns>Confirmation message</returns>
    /// <response code="200">Chat session completed successfully</response>
    /// <response code="404">Chat session not found</response>
    [HttpPost("{sessionId}/complete")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CompleteChatSession(string sessionId)
    {
        var session = await _chatQueueService.GetChatSessionAsync(sessionId);

        if (session == null)
        {
            return NotFound(new { error = "Chat session not found" });
        }

        // Release agent capacity
        if (session.AssignedAgentId != null)
        {
            await _agentAssignmentService.ReleaseChatFromAgentAsync(sessionId);
        }

        await _chatQueueService.CompleteSessionAsync(sessionId);
        _logger.LogInformation("Chat session {SessionId} completed", sessionId);

        return Ok(new { message = "Chat session completed successfully" });
    }
}

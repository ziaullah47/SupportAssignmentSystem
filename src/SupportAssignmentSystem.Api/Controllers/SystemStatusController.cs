using Microsoft.AspNetCore.Mvc;
using SupportAssignmentSystem.Core.Interfaces;

namespace SupportAssignmentSystem.Api.Controllers;

/// <summary>
/// Provides system status and monitoring information
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SystemStatusController : ControllerBase
{
    private readonly ITeamManagementService _teamManagementService;
    private readonly IChatQueueService _chatQueueService;
    private readonly ILogger<SystemStatusController> _logger;

    public SystemStatusController(
        ITeamManagementService teamManagementService,
        IChatQueueService chatQueueService,
        ILogger<SystemStatusController> logger)
    {
        _teamManagementService = teamManagementService;
        _chatQueueService = chatQueueService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current status of all teams and their agents
    /// </summary>
    /// <returns>Detailed information about teams, agents, capacities, and availability</returns>
    /// <response code="200">Team status retrieved successfully</response>
    [HttpGet("teams")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTeamsStatus()
    {
        var teams = await _teamManagementService.GetAllTeamsAsync();

        var teamsStatus = teams.Select(team => new
        {
            team.Id,
            team.Name,
            Shift = team.Shift.ToString(),
            IsOverflow = team.IsOverflowTeam,
            Capacity = team.GetCapacity(),
            MaxQueueLength = team.GetMaxQueueLength(),
            AvailableCapacity = team.GetAvailableCapacity(),
            Agents = team.Agents.Select(agent => new
            {
                agent.Id,
                agent.Name,
                Seniority = agent.Seniority.ToString(),
                MaxConcurrentChats = agent.MaxConcurrentChats,
                CurrentActiveChatCount = agent.ActiveChatSessionIds.Count,
                AvailableCapacity = agent.AvailableCapacity,
                IsActive = agent.IsActive,
                IsEndingShift = agent.IsEndingShift,
                CanAcceptNewChat = agent.CanAcceptNewChat,
                EfficiencyMultiplier = agent.GetEfficiencyMultiplier()
            }).ToList()
        }).ToList();

        return Ok(new
        {
            CurrentTime = DateTime.UtcNow,
            CurrentHour = DateTime.UtcNow.Hour,
            IsOfficeHours = _teamManagementService.IsOfficeHours(),
            Teams = teamsStatus
        });
    }

    /// <summary>
    /// Gets the current status of all queued chat sessions
    /// </summary>
    /// <returns>Information about queued, assigned, and active sessions</returns>
    /// <response code="200">Queue status retrieved successfully</response>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetQueueStatus()
    {
        var queuedSessions = await _chatQueueService.GetQueuedSessionsAsync();

        return Ok(new
        {
            TotalQueuedSessions = queuedSessions.Count,
            MainQueueSessions = queuedSessions.Where(s => !s.IsOverflow).ToList(),
            OverflowQueueSessions = queuedSessions.Where(s => s.IsOverflow).ToList(),
            Sessions = queuedSessions.Select(s => new
            {
                s.Id,
                s.UserId,
                Status = s.Status.ToString(),
                s.AssignedAgentId,
                s.AssignedTeamId,
                s.CreatedAt,
                s.AssignedAt,
                s.LastPollTime,
                TimeSinceLastPoll = DateTime.UtcNow - s.LastPollTime,
                s.MissedPollCount,
                s.IsOverflow
            }).ToList()
        });
    }

    /// <summary>
    /// Health check endpoint to verify the API is running
    /// </summary>
    /// <returns>Health status of the service</returns>
    /// <response code="200">Service is healthy</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public ActionResult GetHealth()
    {
        return Ok(new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Service = "Support Assignment System"
        });
    }
}

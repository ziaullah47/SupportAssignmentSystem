using Microsoft.AspNetCore.Mvc;
using SupportAssignmentSystem.Core.Interfaces;

namespace SupportAssignmentSystem.Api.Controllers;

/// <summary>
/// Diagnostic endpoints for troubleshooting
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DiagnosticsController : ControllerBase
{
    private readonly IChatQueueService _chatQueueService;
    private readonly ITeamManagementService _teamManagementService;
    private readonly IAgentAssignmentService _agentAssignmentService;
    private readonly ILogger<DiagnosticsController> _logger;

    public DiagnosticsController(
        IChatQueueService chatQueueService,
        ITeamManagementService teamManagementService,
        IAgentAssignmentService agentAssignmentService,
        ILogger<DiagnosticsController> logger)
    {
        _chatQueueService = chatQueueService;
        _teamManagementService = teamManagementService;
        _agentAssignmentService = agentAssignmentService;
        _logger = logger;
    }

    /// <summary>
    /// Manually triggers assignment for a specific session (for debugging)
    /// </summary>
    [HttpPost("assign/{sessionId}")]
    public async Task<ActionResult> ManuallyAssignSession(string sessionId)
    {
        var session = await _chatQueueService.GetChatSessionAsync(sessionId);

        if (session == null)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }

        if (session.Status != Core.Enums.ChatSessionStatus.Queued)
        {
            return BadRequest(new 
            { 
                error = "Session is not in Queued status", 
                currentStatus = session.Status.ToString(),
                sessionId 
            });
        }

        // Try to assign
        var assigned = await _agentAssignmentService.AssignChatToAgentAsync(session);

        if (assigned)
        {
            return Ok(new
            {
                success = true,
                message = "Session assigned successfully",
                sessionId,
                assignedAgentId = session.AssignedAgentId,
                assignedTeamId = session.AssignedTeamId,
                assignedAt = session.AssignedAt
            });
        }
        else
        {
            // Get diagnostic info
            var teams = await _teamManagementService.GetAllTeamsAsync();
            var currentHour = DateTime.UtcNow.Hour;
            var shift = GetCurrentShift(currentHour);

            return Ok(new
            {
                success = false,
                message = "No available agents",
                sessionId,
                diagnostics = new
                {
                    currentTime = DateTime.UtcNow,
                    currentHour,
                    currentShift = shift.ToString(),
                    isOfficeHours = _teamManagementService.IsOfficeHours(),
                    sessionIsOverflow = session.IsOverflow,
                    availableAgents = teams
                        .Where(t => !t.IsOverflowTeam && t.Shift == shift)
                        .SelectMany(t => t.Agents)
                        .Where(a => a.IsActive)
                        .Select(a => new
                        {
                            a.Id,
                            a.Name,
                            a.Seniority,
                            a.IsActive,
                            a.IsEndingShift,
                            a.CanAcceptNewChat,
                            a.MaxConcurrentChats,
                            CurrentChats = a.ActiveChatSessionIds.Count,
                            a.AvailableCapacity
                        })
                        .ToList()
                }
            });
        }
    }

    /// <summary>
    /// Gets diagnostic information about why a session might not be assigned
    /// </summary>
    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult> DiagnoseSession(string sessionId)
    {
        var session = await _chatQueueService.GetChatSessionAsync(sessionId);

        if (session == null)
        {
            return NotFound(new { error = "Session not found", sessionId });
        }

        var teams = await _teamManagementService.GetAllTeamsAsync();
        var currentHour = DateTime.UtcNow.Hour;
        var shift = GetCurrentShift(currentHour);

        var activeTeam = teams.FirstOrDefault(t => !t.IsOverflowTeam && t.Shift == shift);

        return Ok(new
        {
            session = new
            {
                session.Id,
                session.UserId,
                Status = session.Status.ToString(),
                session.AssignedAgentId,
                session.AssignedTeamId,
                session.CreatedAt,
                session.AssignedAt,
                session.LastPollTime,
                TimeSinceLastPoll = DateTime.UtcNow - session.LastPollTime,
                session.MissedPollCount,
                session.IsOverflow
            },
            systemInfo = new
            {
                currentTime = DateTime.UtcNow,
                currentHour,
                currentShift = shift.ToString(),
                isOfficeHours = _teamManagementService.IsOfficeHours()
            },
            activeTeamInfo = activeTeam != null ? new
            {
                activeTeam.Id,
                activeTeam.Name,
                Shift = activeTeam.Shift.ToString(),
                Capacity = activeTeam.GetCapacity(),
                AvailableCapacity = activeTeam.GetAvailableCapacity(),
                MaxQueueLength = activeTeam.GetMaxQueueLength(),
                AgentCount = activeTeam.Agents.Count,
                ActiveAgentCount = activeTeam.Agents.Count(a => a.IsActive),
                AvailableAgentCount = activeTeam.Agents.Count(a => a.CanAcceptNewChat)
            } : null,
            availableAgents = teams
                .Where(t => !t.IsOverflowTeam && t.Shift == shift)
                .SelectMany(t => t.Agents)
                .Where(a => a.IsActive)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    Seniority = a.Seniority.ToString(),
                    a.IsActive,
                    a.IsEndingShift,
                    a.CanAcceptNewChat,
                    a.MaxConcurrentChats,
                    CurrentChats = a.ActiveChatSessionIds.Count,
                    a.AvailableCapacity,
                    Reason = !a.CanAcceptNewChat ? 
                        (!a.IsActive ? "Agent is inactive" :
                         a.IsEndingShift ? "Agent is ending shift" :
                         a.AvailableCapacity <= 0 ? "Agent at capacity" : "Unknown") 
                        : "Available"
                })
                .ToList(),
            recommendation = session.AssignedAgentId == null && session.Status == Core.Enums.ChatSessionStatus.Queued
                ? "Session is queued but not assigned. Check if PollingMonitor is running."
                : session.AssignedAgentId != null
                ? "Session is assigned to agent " + session.AssignedAgentId
                : "Session status: " + session.Status.ToString()
        });
    }

    private Core.Enums.ShiftType GetCurrentShift(int hour)
    {
        if (hour >= 0 && hour < 8)
            return Core.Enums.ShiftType.Morning;
        if (hour >= 8 && hour < 16)
            return Core.Enums.ShiftType.Day;
        return Core.Enums.ShiftType.Evening;
    }
}

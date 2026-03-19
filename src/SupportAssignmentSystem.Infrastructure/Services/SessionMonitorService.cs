using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace SupportAssignmentSystem.Infrastructure.Services;

public class SessionMonitorService
{
    private readonly IChatQueueService _chatQueueService;
    private readonly IAgentAssignmentService _agentAssignmentService;
    private readonly ILogger<SessionMonitorService> _logger;

    public SessionMonitorService(
        IChatQueueService chatQueueService,
        IAgentAssignmentService agentAssignmentService,
        ILogger<SessionMonitorService> logger)
    {
        _chatQueueService = chatQueueService;
        _agentAssignmentService = agentAssignmentService;
        _logger = logger;
    }

    public async Task MonitorSessionsAsync(CancellationToken cancellationToken)
    {
        var queuedSessions = await _chatQueueService.GetQueuedSessionsAsync();

        foreach (var session in queuedSessions)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Check for inactive sessions (missed 3 poll requests)
            // Since the monitor runs every 1 second, we check if more than 1 second has passed since last poll
            var timeSinceLastPoll = DateTime.UtcNow - session.LastPollTime;
            if (timeSinceLastPoll.TotalSeconds >= 1)
            {
                // Increment missed poll count
                session.MissedPollCount++;
                
                // Mark inactive if 3 consecutive polls were missed
                if (session.MissedPollCount >= 3)
                {
                    _logger.LogWarning("Session {SessionId} is inactive. Missed {MissedCount} poll requests. Last poll: {LastPoll}", 
                        session.Id, session.MissedPollCount, session.LastPollTime);
                    await _chatQueueService.MarkSessionInactiveAsync(session.Id);
                    
                    // Release agent if assigned
                    if (session.AssignedAgentId != null)
                    {
                        await _agentAssignmentService.ReleaseChatFromAgentAsync(session.Id);
                    }
                    continue;
                }
            }

            // Try to assign unassigned sessions
            if (session.Status == ChatSessionStatus.Queued && session.AssignedAgentId == null)
            {
                var assigned = await _agentAssignmentService.AssignChatToAgentAsync(session);
                if (assigned)
                {
                    _logger.LogInformation("Session {SessionId} assigned to agent {AgentId}", 
                        session.Id, session.AssignedAgentId);
                }
                else
                {
                    _logger.LogDebug("No available agent for session {SessionId}", session.Id);
                }
            }
        }
    }
}

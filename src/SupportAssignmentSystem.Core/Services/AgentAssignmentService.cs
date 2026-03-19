using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;

namespace SupportAssignmentSystem.Core.Services;

public class AgentAssignmentService : IAgentAssignmentService
{
    private readonly ITeamManagementService _teamManagementService;
    private readonly IChatQueueService _chatQueueService;

    public AgentAssignmentService(ITeamManagementService teamManagementService, IChatQueueService chatQueueService)
    {
        _teamManagementService = teamManagementService;
        _chatQueueService = chatQueueService;
    }

    public async Task<bool> AssignChatToAgentAsync(ChatSession session)
    {
        if (session.Status != ChatSessionStatus.Queued)
            return false;

        // Determine which team to use based on overflow flag
        string? teamId = session.AssignedTeamId;
        if (session.IsOverflow && string.IsNullOrEmpty(teamId))
        {
            var overflowTeam = await _teamManagementService.GetOverflowTeamAsync();
            teamId = overflowTeam?.Id;
        }

        var agent = await GetNextAvailableAgentAsync(teamId, session.IsOverflow);
        if (agent == null)
            return false;

        session.AssignedAgentId = agent.Id;
        session.AssignedTeamId = agent.TeamId;
        session.Status = ChatSessionStatus.Assigned;
        session.AssignedAt = DateTime.UtcNow;

        agent.ActiveChatSessionIds.Add(session.Id);

        return true;
    }

    public async Task<Agent?> GetNextAvailableAgentAsync(string? teamId = null, bool isOverflow = false)
    {
        var teams = await _teamManagementService.GetAllTeamsAsync();
        
        List<Agent> availableAgents;
        
        if (!string.IsNullOrEmpty(teamId))
        {
            var team = teams.FirstOrDefault(t => t.Id == teamId);
            if (team == null)
                return null;
            
            availableAgents = team.Agents
                .Where(a => a.CanAcceptNewChat)
                .ToList();
        }
        else if (isOverflow)
        {
            // For overflow sessions, only use overflow team
            var overflowTeam = await _teamManagementService.GetOverflowTeamAsync();
            if (overflowTeam == null)
                return null;
            
            availableAgents = overflowTeam.Agents
                .Where(a => a.CanAcceptNewChat)
                .ToList();
        }
        else
        {
            // For regular sessions, use current shift's team
            var currentShift = GetCurrentShift();
            var activeTeams = teams.Where(t => !t.IsOverflowTeam && t.Shift == currentShift).ToList();
            
            availableAgents = activeTeams
                .SelectMany(t => t.Agents)
                .Where(a => a.CanAcceptNewChat)
                .ToList();
        }

        if (!availableAgents.Any())
            return null;

        // Round-robin assignment: prefer junior first, then mid, then senior, then team lead
        var orderedAgents = availableAgents
            .OrderBy(a => GetSeniorityPriority(a.Seniority))
            .ThenBy(a => a.ActiveChatSessionIds.Count) // Distribute evenly among same seniority
            .ToList();

        return orderedAgents.FirstOrDefault();
    }

    public async Task ReleaseChatFromAgentAsync(string sessionId)
    {
        var session = await _chatQueueService.GetChatSessionAsync(sessionId);
        if (session?.AssignedAgentId == null)
            return;

        var teams = await _teamManagementService.GetAllTeamsAsync();
        var agent = teams
            .SelectMany(t => t.Agents)
            .FirstOrDefault(a => a.Id == session.AssignedAgentId);

        if (agent != null)
        {
            agent.ActiveChatSessionIds.Remove(sessionId);
        }
    }

    private int GetSeniorityPriority(Seniority seniority)
    {
        return seniority switch
        {
            Seniority.Junior => 1,
            Seniority.MidLevel => 2,
            Seniority.Senior => 3,
            Seniority.TeamLead => 4,
            _ => 5
        };
    }

    private ShiftType GetCurrentShift()
    {
        var hour = DateTime.UtcNow.Hour;
        if (hour >= 0 && hour < 8)
            return ShiftType.Morning;
        if (hour >= 8 && hour < 16)
            return ShiftType.Day;
        return ShiftType.Evening;
    }
}

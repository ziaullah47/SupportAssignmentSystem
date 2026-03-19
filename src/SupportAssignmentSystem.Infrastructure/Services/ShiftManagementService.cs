using SupportAssignmentSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace SupportAssignmentSystem.Infrastructure.Services;

public class ShiftManagementService
{
    private readonly ITeamManagementService _teamManagementService;
    private readonly ILogger<ShiftManagementService> _logger;

    public ShiftManagementService(
        ITeamManagementService teamManagementService,
        ILogger<ShiftManagementService> logger)
    {
        _teamManagementService = teamManagementService;
        _logger = logger;
    }

    public async Task ManageShiftTransitionsAsync()
    {
        var teams = await _teamManagementService.GetAllTeamsAsync();
        var currentHour = DateTime.UtcNow.Hour;

        foreach (var team in teams.Where(t => !t.IsOverflowTeam))
        {
            // Determine if the team's shift is ending soon (within last 30 minutes of shift)
            bool isShiftEnding = IsShiftEnding(team.Shift, currentHour);
            bool isShiftOver = IsShiftOver(team.Shift, currentHour);

            foreach (var agent in team.Agents)
            {
                if (isShiftOver)
                {
                    // Shift is completely over - only keep agents active if they have active chats
                    if (agent.ActiveChatSessionIds.Count == 0)
                    {
                        agent.IsActive = false;
                        agent.IsEndingShift = false;
                    }
                }
                else if (isShiftEnding)
                {
                    // Shift is ending soon - mark as ending shift (won't accept new chats)
                    agent.IsEndingShift = true;
                    _logger.LogInformation("Agent {AgentId} ({AgentName}) marked as ending shift", 
                        agent.Id, agent.Name);
                }
                else
                {
                    // During normal shift hours
                    agent.IsActive = true;
                    agent.IsEndingShift = false;
                }
            }
        }
    }

    private bool IsShiftEnding(Core.Enums.ShiftType shift, int currentHour)
    {
        return shift switch
        {
            Core.Enums.ShiftType.Morning => currentHour == 7, // 7:00-7:59 (ending at 8:00)
            Core.Enums.ShiftType.Day => currentHour == 15,    // 15:00-15:59 (ending at 16:00)
            Core.Enums.ShiftType.Evening => currentHour == 23, // 23:00-23:59 (ending at 0:00)
            _ => false
        };
    }

    private bool IsShiftOver(Core.Enums.ShiftType shift, int currentHour)
    {
        return shift switch
        {
            Core.Enums.ShiftType.Morning => currentHour >= 8 && currentHour < 16, // Morning shift ended
            Core.Enums.ShiftType.Day => currentHour >= 16 || currentHour < 8,      // Day shift ended
            Core.Enums.ShiftType.Evening => currentHour >= 0 && currentHour < 8,   // Evening shift ended
            _ => false
        };
    }
}

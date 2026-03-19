using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;

namespace SupportAssignmentSystem.Core.Services;

public class TeamManagementService : ITeamManagementService
{
    private readonly List<Team> _teams = new();
    private bool _initialized;

    public async Task<Team?> GetActiveTeamForShiftAsync(ShiftType shift)
    {
        if (!_initialized)
            await InitializeTeamsAsync();

        return _teams.FirstOrDefault(t => !t.IsOverflowTeam && t.Shift == shift);
    }

    public async Task<Team?> GetOverflowTeamAsync()
    {
        if (!_initialized)
            await InitializeTeamsAsync();

        return _teams.FirstOrDefault(t => t.IsOverflowTeam);
    }

    public async Task<List<Team>> GetAllTeamsAsync()
    {
        if (!_initialized)
            await InitializeTeamsAsync();

        return _teams;
    }

    public bool IsOfficeHours()
    {
        var hour = DateTime.UtcNow.Hour;
        // Office hours: 08:00 - 16:00 (Day shift)
        return hour >= 8 && hour < 16;
    }

    public Task InitializeTeamsAsync()
    {
        if (_initialized)
            return Task.CompletedTask;

        // Team A: 1x team lead, 2x mid-level, 1x junior (Day shift)
        var teamA = new Team
        {
            Id = "team-a",
            Name = "Team A",
            Shift = ShiftType.Day,
            IsOverflowTeam = false
        };
        teamA.Agents.Add(new Agent { Id = "agent-a1", Name = "Agent A1", Seniority = Seniority.TeamLead, TeamId = teamA.Id, Shift = ShiftType.Day });
        teamA.Agents.Add(new Agent { Id = "agent-a2", Name = "Agent A2", Seniority = Seniority.MidLevel, TeamId = teamA.Id, Shift = ShiftType.Day });
        teamA.Agents.Add(new Agent { Id = "agent-a3", Name = "Agent A3", Seniority = Seniority.MidLevel, TeamId = teamA.Id, Shift = ShiftType.Day });
        teamA.Agents.Add(new Agent { Id = "agent-a4", Name = "Agent A4", Seniority = Seniority.Junior, TeamId = teamA.Id, Shift = ShiftType.Day });

        // Team B: 1x senior, 1x mid-level, 2x junior (Evening shift)
        var teamB = new Team
        {
            Id = "team-b",
            Name = "Team B",
            Shift = ShiftType.Evening,
            IsOverflowTeam = false
        };
        teamB.Agents.Add(new Agent { Id = "agent-b1", Name = "Agent B1", Seniority = Seniority.Senior, TeamId = teamB.Id, Shift = ShiftType.Evening });
        teamB.Agents.Add(new Agent { Id = "agent-b2", Name = "Agent B2", Seniority = Seniority.MidLevel, TeamId = teamB.Id, Shift = ShiftType.Evening });
        teamB.Agents.Add(new Agent { Id = "agent-b3", Name = "Agent B3", Seniority = Seniority.Junior, TeamId = teamB.Id, Shift = ShiftType.Evening });
        teamB.Agents.Add(new Agent { Id = "agent-b4", Name = "Agent B4", Seniority = Seniority.Junior, TeamId = teamB.Id, Shift = ShiftType.Evening });

        // Team C: 2x mid-level (Night shift)
        var teamC = new Team
        {
            Id = "team-c",
            Name = "Team C",
            Shift = ShiftType.Morning,
            IsOverflowTeam = false
        };
        teamC.Agents.Add(new Agent { Id = "agent-c1", Name = "Agent C1", Seniority = Seniority.MidLevel, TeamId = teamC.Id, Shift = ShiftType.Morning });
        teamC.Agents.Add(new Agent { Id = "agent-c2", Name = "Agent C2", Seniority = Seniority.MidLevel, TeamId = teamC.Id, Shift = ShiftType.Morning });

        // Overflow team: 6x junior
        var overflowTeam = new Team
        {
            Id = "team-overflow",
            Name = "Overflow Team",
            Shift = ShiftType.Day,
            IsOverflowTeam = true
        };
        for (int i = 1; i <= 6; i++)
        {
            overflowTeam.Agents.Add(new Agent
            {
                Id = $"agent-overflow{i}",
                Name = $"Overflow Agent {i}",
                Seniority = Seniority.Junior,
                TeamId = overflowTeam.Id,
                Shift = ShiftType.Day,
                IsActive = false // Overflow agents are inactive by default
            });
        }

        _teams.Add(teamA);
        _teams.Add(teamB);
        _teams.Add(teamC);
        _teams.Add(overflowTeam);

        _initialized = true;
        return Task.CompletedTask;
    }
}

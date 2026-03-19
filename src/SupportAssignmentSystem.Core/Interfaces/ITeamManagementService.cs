using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;

namespace SupportAssignmentSystem.Core.Interfaces;

public interface ITeamManagementService
{
    Task<Team?> GetActiveTeamForShiftAsync(ShiftType shift);
    Task<Team?> GetOverflowTeamAsync();
    Task<List<Team>> GetAllTeamsAsync();
    Task InitializeTeamsAsync();
    bool IsOfficeHours();
}

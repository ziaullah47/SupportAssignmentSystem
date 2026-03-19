using SupportAssignmentSystem.Core.Enums;

namespace SupportAssignmentSystem.Core.Entities;

public class Agent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public Seniority Seniority { get; set; }
    public string TeamId { get; set; } = string.Empty;
    public ShiftType Shift { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEndingShift { get; set; }
    public List<string> ActiveChatSessionIds { get; set; } = new();

    public int MaxConcurrentChats => (int)Math.Floor(10 * GetEfficiencyMultiplier());

    public int AvailableCapacity => MaxConcurrentChats - ActiveChatSessionIds.Count;

    public bool CanAcceptNewChat => IsActive && !IsEndingShift && AvailableCapacity > 0;

    public double GetEfficiencyMultiplier()
    {
        return Seniority switch
        {
            Seniority.Junior => 0.4,
            Seniority.MidLevel => 0.6,
            Seniority.Senior => 0.8,
            Seniority.TeamLead => 0.5,
            _ => 0.4
        };
    }
}

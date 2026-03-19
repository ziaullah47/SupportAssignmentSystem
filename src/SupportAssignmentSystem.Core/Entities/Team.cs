using SupportAssignmentSystem.Core.Enums;

namespace SupportAssignmentSystem.Core.Entities;

public class Team
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public List<Agent> Agents { get; set; } = new();
    public ShiftType Shift { get; set; }
    public bool IsOverflowTeam { get; set; }

    public int GetCapacity()
    {
        return (int)Math.Floor(Agents
            .Where(a => a.IsActive)
            .Sum(a => 10 * a.GetEfficiencyMultiplier()));
    }

    public int GetMaxQueueLength()
    {
        return (int)Math.Floor(GetCapacity() * 1.5);
    }

    public int GetAvailableCapacity()
    {
        return Agents
            .Where(a => a.CanAcceptNewChat)
            .Sum(a => a.AvailableCapacity);
    }
}

using Microsoft.EntityFrameworkCore;
using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;

namespace SupportAssignmentSystem.Infrastructure.Data;

public class SupportAssignmentDbContext : DbContext
{
    public SupportAssignmentDbContext(DbContextOptions<SupportAssignmentDbContext> options)
        : base(options)
    {
    }

    public DbSet<ChatSessionEntity> ChatSessions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatSessionEntity>(entity =>
        {
            entity.ToTable("ChatSessions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.UserId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.AssignedAgentId).HasMaxLength(50);
            entity.Property(e => e.AssignedTeamId).HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsOverflow);
        });
    }
}

/// <summary>
/// Database entity for ChatSession
/// </summary>
public class ChatSessionEntity
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public ChatSessionStatus Status { get; set; }
    public string? AssignedAgentId { get; set; }
    public string? AssignedTeamId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime LastPollTime { get; set; }
    public int MissedPollCount { get; set; }
    public bool IsOverflow { get; set; }

    public ChatSession ToEntity()
    {
        return new ChatSession
        {
            Id = Id,
            UserId = UserId,
            Status = Status,
            AssignedAgentId = AssignedAgentId,
            AssignedTeamId = AssignedTeamId,
            CreatedAt = CreatedAt,
            AssignedAt = AssignedAt,
            LastPollTime = LastPollTime,
            MissedPollCount = MissedPollCount,
            IsOverflow = IsOverflow
        };
    }

    public static ChatSessionEntity FromEntity(ChatSession session)
    {
        return new ChatSessionEntity
        {
            Id = session.Id,
            UserId = session.UserId,
            Status = session.Status,
            AssignedAgentId = session.AssignedAgentId,
            AssignedTeamId = session.AssignedTeamId,
            CreatedAt = session.CreatedAt,
            AssignedAt = session.AssignedAt,
            LastPollTime = session.LastPollTime,
            MissedPollCount = session.MissedPollCount,
            IsOverflow = session.IsOverflow
        };
    }
}

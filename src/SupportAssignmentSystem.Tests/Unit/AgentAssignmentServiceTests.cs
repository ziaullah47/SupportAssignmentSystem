using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;
using SupportAssignmentSystem.Core.Services;
using SupportAssignmentSystem.Infrastructure.Storage;

namespace SupportAssignmentSystem.Tests.Unit;

/// <summary>
/// Unit tests for AgentAssignmentService
/// Tests round-robin assignment logic and capacity management
/// </summary>
public class AgentAssignmentServiceTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IAgentAssignmentService _agentAssignmentService = null!;
    private ITeamManagementService _teamManagementService = null!;
    private IChatQueueService _chatQueueService = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISessionStorage, InMemorySessionStorage>();
        services.AddSingleton<ITeamManagementService, TeamManagementService>();
        services.AddSingleton<IChatQueueService, ChatQueueService>();
        services.AddSingleton<IAgentAssignmentService, AgentAssignmentService>();
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _agentAssignmentService = _serviceProvider.GetRequiredService<IAgentAssignmentService>();
        _teamManagementService = _serviceProvider.GetRequiredService<ITeamManagementService>();
        _chatQueueService = _serviceProvider.GetRequiredService<IChatQueueService>();

        await _teamManagementService.InitializeTeamsAsync();
    }

    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AssignChat_ToQueuedSession_ShouldAssignAgent()
    {
        // Arrange
        var session = new ChatSession
        {
            Id = "test-session",
            Status = ChatSessionStatus.Queued,
            UserId = "user1"
        };

        // Act
        var result = await _agentAssignmentService.AssignChatToAgentAsync(session);

        // Assert
        result.Should().BeTrue();
        session.Status.Should().Be(ChatSessionStatus.Assigned);
        session.AssignedAgentId.Should().NotBeNullOrEmpty();
        session.AssignedTeamId.Should().NotBeNullOrEmpty();
        session.AssignedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignChat_ToAlreadyAssignedSession_ShouldReturnFalse()
    {
        // Arrange
        var session = new ChatSession
        {
            Id = "assigned-session",
            Status = ChatSessionStatus.Assigned,
            UserId = "user1",
            AssignedAgentId = "agent-1"
        };

        // Act
        var result = await _agentAssignmentService.AssignChatToAgentAsync(session);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetNextAvailableAgent_ShouldReturnJuniorFirst()
    {
        // Act
        var agent = await _agentAssignmentService.GetNextAvailableAgentAsync();

        // Assert
        agent.Should().NotBeNull();
        // During day shift (8-16), should get agent from Team A
        // Junior agent (agent-a4) should be assigned first
        agent!.Seniority.Should().Be(Seniority.Junior);
        agent.Id.Should().Contain("agent-a4");
    }

    [Fact]
    public async Task AssignMultipleSessions_ShouldDistributeAcrossAgents()
    {
        // Arrange - Create 10 sessions
        var sessions = Enumerable.Range(1, 10)
            .Select(i => new ChatSession
            {
                Id = $"session-{i}",
                Status = ChatSessionStatus.Queued,
                UserId = $"user{i}"
            })
            .ToList();

        // Act - Assign all sessions
        foreach (var session in sessions)
        {
            await _agentAssignmentService.AssignChatToAgentAsync(session);
        }

        // Assert
        var agentAssignments = sessions
            .GroupBy(s => s.AssignedAgentId)
            .ToDictionary(g => g.Key!, g => g.Count());

        // Should have distributed across multiple agents
        agentAssignments.Count.Should().BeGreaterThan(1);

        // First agent (junior) should have the most assignments initially
        var juniorAgent = sessions.First().AssignedAgentId;
        juniorAgent.Should().Contain("agent-a4");
    }

    [Fact]
    public async Task ReleaseChatFromAgent_ShouldFreeCapacity()
    {
        // Arrange - Assign a session
        var session = await _chatQueueService.EnqueueChatSessionAsync("user1");
        await _agentAssignmentService.AssignChatToAgentAsync(session!);
        var agentId = session!.AssignedAgentId;

        // Act - Release the agent
        await _agentAssignmentService.ReleaseChatFromAgentAsync(session.Id);

        // Assert - Agent should now have capacity for another chat
        var agent = await _agentAssignmentService.GetNextAvailableAgentAsync();
        agent.Should().NotBeNull();
        agent!.Id.Should().Be(agentId); // Same agent should be available again
    }

    [Fact]
    public async Task AgentCapacity_ShouldRespectMaxConcurrentChats()
    {
        // Arrange - Get a junior agent (capacity 4)
        var teams = await _teamManagementService.GetAllTeamsAsync();
        var juniorAgent = teams
            .SelectMany(t => t.Agents)
            .First(a => a.Seniority == Seniority.Junior);

        var initialCapacity = juniorAgent.AvailableCapacity;

        // Act - Assign chats up to capacity
        for (int i = 0; i < juniorAgent.MaxConcurrentChats; i++)
        {
            var session = new ChatSession
            {
                Id = $"session-{i}",
                Status = ChatSessionStatus.Queued,
                UserId = $"user{i}"
            };
            await _agentAssignmentService.AssignChatToAgentAsync(session);
        }

        // Assert
        juniorAgent.AvailableCapacity.Should().Be(0);
        juniorAgent.CanAcceptNewChat.Should().BeFalse();
    }

    [Fact]
    public async Task RoundRobinAssignment_ShouldFollowSeniorityOrder()
    {
        // Arrange - Create sessions
        var sessions = Enumerable.Range(1, 8)
            .Select(i => new ChatSession
            {
                Id = $"session-{i}",
                Status = ChatSessionStatus.Queued,
                UserId = $"user{i}"
            })
            .ToList();

        // Act - Assign all
        foreach (var session in sessions)
        {
            await _agentAssignmentService.AssignChatToAgentAsync(session);
        }

        // Assert - Check assignment order
        var assignments = sessions
            .Select(s => new { s.Id, s.AssignedAgentId })
            .ToList();

        // First sessions should go to junior (lowest seniority)
        sessions.Take(4).Should().AllSatisfy(s =>
        {
            s.AssignedAgentId.Should().Contain("agent-a4"); // Junior
        });

        // Next sessions should go to mid-level agents
        sessions.Skip(4).Take(4).Should().AllSatisfy(s =>
        {
            s.AssignedAgentId.Should().Match(id =>
                id!.Contains("agent-a2") || id.Contains("agent-a3")); // Mid-level agents
        });
    }
}

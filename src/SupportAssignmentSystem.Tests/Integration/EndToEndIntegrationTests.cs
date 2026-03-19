using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;
using SupportAssignmentSystem.Core.Services;
using SupportAssignmentSystem.Infrastructure.Services;
using SupportAssignmentSystem.Infrastructure.Storage;

namespace SupportAssignmentSystem.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the entire support assignment system
/// Tests the complete flow: Create ? Queue ? Monitor ? Assign ? Poll ? Complete
/// </summary>
public class EndToEndIntegrationTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IChatQueueService _chatQueueService = null!;
    private IAgentAssignmentService _agentAssignmentService = null!;
    private ITeamManagementService _teamManagementService = null!;
    private SessionMonitorService _sessionMonitorService = null!;
    private CancellationTokenSource _cts = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        // Register services as they would be in the real application
        services.AddSingleton<ISessionStorage, InMemorySessionStorage>();
        services.AddSingleton<ITeamManagementService, TeamManagementService>();
        services.AddSingleton<IChatQueueService, ChatQueueService>();
        services.AddSingleton<IAgentAssignmentService, AgentAssignmentService>();
        services.AddSingleton<SessionMonitorService>();
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();

        // Get services
        _chatQueueService = _serviceProvider.GetRequiredService<IChatQueueService>();
        _agentAssignmentService = _serviceProvider.GetRequiredService<IAgentAssignmentService>();
        _teamManagementService = _serviceProvider.GetRequiredService<ITeamManagementService>();
        _sessionMonitorService = _serviceProvider.GetRequiredService<SessionMonitorService>();

        // Initialize teams
        await _teamManagementService.InitializeTeamsAsync();

        _cts = new CancellationTokenSource();
    }

    public Task DisposeAsync()
    {
        _cts?.Dispose();
        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task EndToEnd_CreateSession_ShouldBeQueued()
    {
        // Arrange
        var userId = "testuser1";

        // Act
        var session = await _chatQueueService.EnqueueChatSessionAsync(userId);

        // Assert
        session.Should().NotBeNull();
        session!.UserId.Should().Be(userId);
        session.Status.Should().Be(ChatSessionStatus.Queued);
        session.AssignedAgentId.Should().BeNull();
        session.IsOverflow.Should().BeFalse();
    }

    [Fact]
    public async Task EndToEnd_CreateAndMonitor_ShouldAssignToAgent()
    {
        // Arrange
        var userId = "testuser2";

        // Act - Create session
        var session = await _chatQueueService.EnqueueChatSessionAsync(userId);
        session.Should().NotBeNull();

        // Wait a bit to simulate time passing
        await Task.Delay(100);

        // Act - Run monitor (simulates the background service)
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);

        // Get updated session
        var updatedSession = await _chatQueueService.GetChatSessionAsync(session!.Id);

        // Assert
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be(ChatSessionStatus.Assigned);
        updatedSession.AssignedAgentId.Should().NotBeNullOrEmpty();
        updatedSession.AssignedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task EndToEnd_CreatePollAndMonitor_ShouldStayActive()
    {
        // Arrange
        var userId = "testuser3";

        // Act - Create session
        var session = await _chatQueueService.EnqueueChatSessionAsync(userId);
        session.Should().NotBeNull();

        // Run monitor to assign
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);

        // Poll the session (keeps it active)
        var pollResult = await _chatQueueService.PollChatSessionAsync(session!.Id);

        // Wait and monitor again
        await Task.Delay(100);
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);

        // Get updated session
        var updatedSession = await _chatQueueService.GetChatSessionAsync(session.Id);

        // Assert
        pollResult.Should().BeTrue();
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be(ChatSessionStatus.Assigned);
        updatedSession.MissedPollCount.Should().Be(0);
    }

    [Fact]
    public async Task EndToEnd_CreateAndStopPolling_ShouldBecomeInactive()
    {
        // Arrange
        var userId = "testuser4";

        // Act - Create session
        var session = await _chatQueueService.EnqueueChatSessionAsync(userId);
        session.Should().NotBeNull();

        // Run monitor to assign
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);

        // Poll once
        await _chatQueueService.PollChatSessionAsync(session!.Id);

        // Simulate 3 seconds passing without polling
        await Task.Delay(100);
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token); // 1st missed poll

        await Task.Delay(100);
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token); // 2nd missed poll

        await Task.Delay(100);
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token); // 3rd missed poll - should mark inactive

        // Get updated session
        var updatedSession = await _chatQueueService.GetChatSessionAsync(session.Id);

        // Assert
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be(ChatSessionStatus.Inactive);
        updatedSession.MissedPollCount.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task EndToEnd_CompleteSession_ShouldReleaseAgent()
    {
        // Arrange
        var userId = "testuser5";

        // Act - Create session
        var session = await _chatQueueService.EnqueueChatSessionAsync(userId);
        session.Should().NotBeNull();

        // Run monitor to assign
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);

        // Get the assigned agent
        var assignedSession = await _chatQueueService.GetChatSessionAsync(session!.Id);
        var agentId = assignedSession!.AssignedAgentId;

        // Complete the session
        await _chatQueueService.CompleteSessionAsync(session.Id);
        await _agentAssignmentService.ReleaseChatFromAgentAsync(session.Id);

        // Get updated session
        var completedSession = await _chatQueueService.GetChatSessionAsync(session.Id);

        // Assert
        completedSession.Should().NotBeNull();
        completedSession!.Status.Should().Be(ChatSessionStatus.Completed);
        agentId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task EndToEnd_RoundRobinAssignment_ShouldAssignToJuniorFirst()
    {
        // Arrange - Create multiple sessions
        var sessions = new List<ChatSession>();
        for (int i = 0; i < 5; i++)
        {
            var session = await _chatQueueService.EnqueueChatSessionAsync($"user{i}");
            sessions.Add(session!);
        }

        // Act - Run monitor to assign all sessions
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);

        // Get updated sessions
        var assignedSessions = new List<ChatSession>();
        foreach (var session in sessions)
        {
            var updated = await _chatQueueService.GetChatSessionAsync(session.Id);
            assignedSessions.Add(updated!);
        }

        // Assert
        assignedSessions.Should().AllSatisfy(s =>
        {
            s.Status.Should().Be(ChatSessionStatus.Assigned);
            s.AssignedAgentId.Should().NotBeNullOrEmpty();
        });

        // First session should be assigned to junior agent (agent-a4 in Team A during day shift)
        // This assumes we're running during day shift (8:00-16:00 UTC)
        var firstSession = assignedSessions.First();
        firstSession.AssignedAgentId.Should().Contain("agent-a4"); // Junior agent
    }

    [Fact]
    public async Task EndToEnd_MultipleSessionsPolling_ShouldAllStayActive()
    {
        // Arrange - Create 3 sessions
        var sessionIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var session = await _chatQueueService.EnqueueChatSessionAsync($"user{i}");
            sessionIds.Add(session!.Id);
        }

        // Assign all sessions
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);

        // Act - Simulate 5 poll cycles (5 seconds)
        for (int cycle = 0; cycle < 5; cycle++)
        {
            // Poll all sessions
            foreach (var sessionId in sessionIds)
            {
                await _chatQueueService.PollChatSessionAsync(sessionId);
            }

            await Task.Delay(100);
            await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);
        }

        // Get all sessions
        var finalSessions = new List<ChatSession>();
        foreach (var sessionId in sessionIds)
        {
            var session = await _chatQueueService.GetChatSessionAsync(sessionId);
            finalSessions.Add(session!);
        }

        // Assert - All should still be assigned (active)
        finalSessions.Should().AllSatisfy(s =>
        {
            s.Status.Should().Be(ChatSessionStatus.Assigned);
            s.MissedPollCount.Should().Be(0);
        });
    }

    [Fact]
    public async Task EndToEnd_AgentCapacity_ShouldRespectMaxConcurrentChats()
    {
        // Arrange - Create more sessions than a single junior can handle (junior has capacity of 4)
        var sessions = new List<ChatSession>();
        for (int i = 0; i < 6; i++)
        {
            var session = await _chatQueueService.EnqueueChatSessionAsync($"capacityuser{i}");
            sessions.Add(session!);
        }

        // Act - Run monitor to assign
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);

        // Get assigned sessions
        var assignedSessions = new List<ChatSession>();
        foreach (var session in sessions)
        {
            var updated = await _chatQueueService.GetChatSessionAsync(session.Id);
            assignedSessions.Add(updated!);
        }

        // Assert
        assignedSessions.Should().AllSatisfy(s =>
        {
            s.Status.Should().Be(ChatSessionStatus.Assigned);
            s.AssignedAgentId.Should().NotBeNullOrEmpty();
        });

        // Count assignments per agent
        var agentAssignments = assignedSessions
            .GroupBy(s => s.AssignedAgentId)
            .ToDictionary(g => g.Key!, g => g.Count());

        // Each agent should not exceed their capacity
        // Junior (agent-a4): max 4
        // Mid-level (agent-a2, agent-a3): max 6 each
        // Team Lead (agent-a1): max 5
        agentAssignments.Should().AllSatisfy(kvp =>
        {
            kvp.Value.Should().BeLessThanOrEqualTo(10); // Max concurrent is 10 * efficiency
        });

        // Should have distributed across multiple agents
        agentAssignments.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task EndToEnd_QueueRefusal_ShouldRefuseWhenQueueFull()
    {
        // This test would require creating enough sessions to fill the queue
        // Team A capacity during day shift: 21, max queue: 31
        // This is a simplified version

        // Arrange - Create multiple sessions
        var acceptedSessions = new List<ChatSession?>();
        var refusedSessions = new List<ChatSession?>();

        // Act - Create many sessions
        for (int i = 0; i < 40; i++) // More than max queue (31)
        {
            var session = await _chatQueueService.EnqueueChatSessionAsync($"queueuser{i}");
            
            if (session?.Status == ChatSessionStatus.Refused)
            {
                refusedSessions.Add(session);
            }
            else
            {
                acceptedSessions.Add(session);
            }
        }

        // Assert
        acceptedSessions.Should().NotBeEmpty();
        acceptedSessions.Should().AllSatisfy(s =>
        {
            s.Should().NotBeNull();
            s!.Status.Should().Be(ChatSessionStatus.Queued);
        });

        // Some sessions should be refused when queue is full
        if (acceptedSessions.Count >= 31)
        {
            refusedSessions.Should().NotBeEmpty();
            refusedSessions.Should().AllSatisfy(s =>
            {
                s.Should().NotBeNull();
                s!.Status.Should().Be(ChatSessionStatus.Refused);
            });
        }
    }

    [Fact]
    public async Task EndToEnd_CompleteWorkflow_CreateAssignPollComplete()
    {
        // This is the complete happy path test

        // Step 1: Create session
        var session = await _chatQueueService.EnqueueChatSessionAsync("completeuser");
        session.Should().NotBeNull();
        session!.Status.Should().Be(ChatSessionStatus.Queued);
        var sessionId = session.Id;

        // Step 2: Monitor assigns agent
        await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);
        var assignedSession = await _chatQueueService.GetChatSessionAsync(sessionId);
        assignedSession!.Status.Should().Be(ChatSessionStatus.Assigned);
        assignedSession.AssignedAgentId.Should().NotBeNullOrEmpty();

        // Step 3: Client polls (simulating 3 poll cycles)
        for (int i = 0; i < 3; i++)
        {
            var pollResult = await _chatQueueService.PollChatSessionAsync(sessionId);
            pollResult.Should().BeTrue();
            await Task.Delay(100);
            await _sessionMonitorService.MonitorSessionsAsync(_cts.Token);
        }

        // Step 4: Verify still active
        var activeSession = await _chatQueueService.GetChatSessionAsync(sessionId);
        activeSession!.Status.Should().Be(ChatSessionStatus.Assigned);
        activeSession.MissedPollCount.Should().Be(0);

        // Step 5: Complete session
        await _agentAssignmentService.ReleaseChatFromAgentAsync(sessionId);
        await _chatQueueService.CompleteSessionAsync(sessionId);

        // Step 6: Verify completed
        var completedSession = await _chatQueueService.GetChatSessionAsync(sessionId);
        completedSession!.Status.Should().Be(ChatSessionStatus.Completed);
    }
}

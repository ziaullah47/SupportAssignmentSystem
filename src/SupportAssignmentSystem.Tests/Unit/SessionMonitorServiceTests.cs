using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SupportAssignmentSystem.Core.Entities;
using SupportAssignmentSystem.Core.Enums;
using SupportAssignmentSystem.Core.Interfaces;
using SupportAssignmentSystem.Infrastructure.Services;

namespace SupportAssignmentSystem.Tests.Unit;

/// <summary>
/// Unit tests for SessionMonitorService
/// Tests the monitoring logic in isolation
/// </summary>
public class SessionMonitorServiceTests
{
    private readonly Mock<IChatQueueService> _mockChatQueueService;
    private readonly Mock<IAgentAssignmentService> _mockAgentAssignmentService;
    private readonly Mock<ILogger<SessionMonitorService>> _mockLogger;
    private readonly SessionMonitorService _service;

    public SessionMonitorServiceTests()
    {
        _mockChatQueueService = new Mock<IChatQueueService>();
        _mockAgentAssignmentService = new Mock<IAgentAssignmentService>();
        _mockLogger = new Mock<ILogger<SessionMonitorService>>();

        _service = new SessionMonitorService(
            _mockChatQueueService.Object,
            _mockAgentAssignmentService.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task MonitorSessions_WithQueuedSession_ShouldAttemptAssignment()
    {
        // Arrange
        var session = new ChatSession
        {
            Id = "test-session",
            Status = ChatSessionStatus.Queued,
            LastPollTime = DateTime.UtcNow,
            AssignedAgentId = null
        };

        _mockChatQueueService
            .Setup(x => x.GetQueuedSessionsAsync())
            .ReturnsAsync(new List<ChatSession> { session });

        _mockAgentAssignmentService
            .Setup(x => x.AssignChatToAgentAsync(It.IsAny<ChatSession>()))
            .ReturnsAsync(true);

        // Act
        await _service.MonitorSessionsAsync(CancellationToken.None);

        // Assert
        _mockAgentAssignmentService.Verify(
            x => x.AssignChatToAgentAsync(It.Is<ChatSession>(s => s.Id == "test-session")),
            Times.Once);
    }

    [Fact]
    public async Task MonitorSessions_WithInactiveSession_ShouldMarkInactive()
    {
        // Arrange
        var session = new ChatSession
        {
            Id = "inactive-session",
            Status = ChatSessionStatus.Assigned,
            LastPollTime = DateTime.UtcNow.AddSeconds(-10), // 10 seconds ago
            MissedPollCount = 0,
            AssignedAgentId = "agent-1"
        };

        _mockChatQueueService
            .Setup(x => x.GetQueuedSessionsAsync())
            .ReturnsAsync(new List<ChatSession> { session });

        // Act - Run monitor 3 times to accumulate missed polls
        await _service.MonitorSessionsAsync(CancellationToken.None);
        await _service.MonitorSessionsAsync(CancellationToken.None);
        await _service.MonitorSessionsAsync(CancellationToken.None);

        // Assert
        _mockChatQueueService.Verify(
            x => x.MarkSessionInactiveAsync(session.Id),
            Times.Once);

        _mockAgentAssignmentService.Verify(
            x => x.ReleaseChatFromAgentAsync(session.Id),
            Times.Once);
    }

    [Fact]
    public async Task MonitorSessions_WithActivelyPolledSession_ShouldNotMarkInactive()
    {
        // Arrange
        var session = new ChatSession
        {
            Id = "active-session",
            Status = ChatSessionStatus.Assigned,
            LastPollTime = DateTime.UtcNow, // Just polled
            MissedPollCount = 0,
            AssignedAgentId = "agent-1"
        };

        _mockChatQueueService
            .Setup(x => x.GetQueuedSessionsAsync())
            .ReturnsAsync(new List<ChatSession> { session });

        // Act
        await _service.MonitorSessionsAsync(CancellationToken.None);

        // Assert
        _mockChatQueueService.Verify(
            x => x.MarkSessionInactiveAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task MonitorSessions_WithNoSessions_ShouldNotThrow()
    {
        // Arrange
        _mockChatQueueService
            .Setup(x => x.GetQueuedSessionsAsync())
            .ReturnsAsync(new List<ChatSession>());

        // Act & Assert
        var act = () => _service.MonitorSessionsAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MonitorSessions_WithCancellationToken_ShouldStopProcessing()
    {
        // Arrange
        var sessions = new List<ChatSession>
        {
            new() { Id = "session-1", Status = ChatSessionStatus.Queued },
            new() { Id = "session-2", Status = ChatSessionStatus.Queued },
            new() { Id = "session-3", Status = ChatSessionStatus.Queued }
        };

        _mockChatQueueService
            .Setup(x => x.GetQueuedSessionsAsync())
            .ReturnsAsync(sessions);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        await _service.MonitorSessionsAsync(cts.Token);

        // Assert - Should not attempt any assignments due to cancellation
        _mockAgentAssignmentService.Verify(
            x => x.AssignChatToAgentAsync(It.IsAny<ChatSession>()),
            Times.Never);
    }
}

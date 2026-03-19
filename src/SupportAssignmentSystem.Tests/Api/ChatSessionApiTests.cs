using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SupportAssignmentSystem.Api.Models;
using SupportAssignmentSystem.Core.Interfaces;

namespace SupportAssignmentSystem.Tests.Api;

/// <summary>
/// API integration tests using WebApplicationFactory
/// Tests the actual HTTP endpoints end-to-end
/// </summary>
public class ChatSessionApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ChatSessionApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Override with InMemory storage for tests
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ISessionStorage));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task CreateChatSession_WithValidUserId_ShouldReturn200()
    {
        // Arrange
        var request = new CreateChatSessionRequest { UserId = "apitest1" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chatsession", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<ChatSessionResponse>();
        content.Should().NotBeNull();
        content!.Id.Should().NotBeNullOrEmpty();
        content.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task CreateChatSession_WithEmptyUserId_ShouldReturn400()
    {
        // Arrange
        var request = new CreateChatSessionRequest { UserId = "" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/chatsession", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetChatSession_WithValidId_ShouldReturn200()
    {
        // Arrange - Create a session first
        var createRequest = new CreateChatSessionRequest { UserId = "apitest2" };
        var createResponse = await _client.PostAsJsonAsync("/api/chatsession", createRequest);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSessionResponse>();

        // Act
        var response = await _client.GetAsync($"/api/chatsession/{session!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<ChatSessionResponse>();
        content.Should().NotBeNull();
        content!.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task GetChatSession_WithInvalidId_ShouldReturn404()
    {
        // Act
        var response = await _client.GetAsync("/api/chatsession/invalid-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PollChatSession_WithValidId_ShouldReturn200()
    {
        // Arrange - Create a session
        var createRequest = new CreateChatSessionRequest { UserId = "apitest3" };
        var createResponse = await _client.PostAsJsonAsync("/api/chatsession", createRequest);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSessionResponse>();

        // Act
        var response = await _client.PostAsync($"/api/chatsession/{session!.Id}/poll", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadFromJsonAsync<PollResponse>();
        content.Should().NotBeNull();
        content!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task PollChatSession_WithInvalidId_ShouldReturn404()
    {
        // Act
        var response = await _client.PostAsync("/api/chatsession/invalid-id/poll", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CompleteChatSession_WithValidId_ShouldReturn200()
    {
        // Arrange - Create a session
        var createRequest = new CreateChatSessionRequest { UserId = "apitest4" };
        var createResponse = await _client.PostAsJsonAsync("/api/chatsession", createRequest);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSessionResponse>();

        // Act
        var response = await _client.PostAsync($"/api/chatsession/{session!.Id}/complete", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EndToEndApi_CreatePollAndMonitor_ShouldGetAssigned()
    {
        // Arrange
        var request = new CreateChatSessionRequest { UserId = "e2euser" };

        // Step 1: Create session
        var createResponse = await _client.PostAsJsonAsync("/api/chatsession", request);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = await createResponse.Content.ReadFromJsonAsync<ChatSessionResponse>();

        // Step 2: Wait for monitor to assign (background service runs every 1s)
        await Task.Delay(2000);

        // Step 3: Poll to check assignment
        var pollResponse = await _client.PostAsync($"/api/chatsession/{session!.Id}/poll", null);
        pollResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pollResult = await pollResponse.Content.ReadFromJsonAsync<PollResponse>();

        // Step 4: Get session details
        var getResponse = await _client.GetAsync($"/api/chatsession/{session.Id}");
        var updatedSession = await getResponse.Content.ReadFromJsonAsync<ChatSessionResponse>();

        // Assert
        pollResult.Should().NotBeNull();
        updatedSession.Should().NotBeNull();
        updatedSession!.Status.Should().Be("Assigned");
        updatedSession.AssignedAgentId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetSystemStatus_Teams_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/api/systemstatus/teams");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Team A");
        content.Should().Contain("Team B");
        content.Should().Contain("Team C");
    }

    [Fact]
    public async Task GetSystemStatus_Queue_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/api/systemstatus/queue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSystemStatus_Health_ShouldReturn200()
    {
        // Act
        var response = await _client.GetAsync("/api/systemstatus/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy");
    }

    [Fact]
    public async Task MultipleSessionsApi_ShouldAllBeCreated()
    {
        // Arrange
        var userIds = Enumerable.Range(1, 5).Select(i => $"multiuser{i}").ToList();

        // Act - Create multiple sessions
        var sessions = new List<ChatSessionResponse>();
        foreach (var userId in userIds)
        {
            var request = new CreateChatSessionRequest { UserId = userId };
            var response = await _client.PostAsJsonAsync("/api/chatsession", request);
            var session = await response.Content.ReadFromJsonAsync<ChatSessionResponse>();
            sessions.Add(session!);
        }

        // Assert
        sessions.Should().HaveCount(5);
        sessions.Should().AllSatisfy(s =>
        {
            s.Id.Should().NotBeNullOrEmpty();
            s.Status.Should().Be("Queued");
        });

        // All sessions should have unique IDs
        sessions.Select(s => s.Id).Distinct().Should().HaveCount(5);
    }
}

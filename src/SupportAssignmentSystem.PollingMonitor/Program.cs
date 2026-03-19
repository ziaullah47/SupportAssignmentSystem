using SupportAssignmentSystem.PollingMonitor;
using SupportAssignmentSystem.Core.Configuration;
using SupportAssignmentSystem.Core.Interfaces;
using SupportAssignmentSystem.Core.Services;
using SupportAssignmentSystem.Infrastructure.Extensions;
using SupportAssignmentSystem.Infrastructure.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configure storage (InMemory, Redis, or Database) based on appsettings.json
await builder.Services.AddStorageServicesAsync(builder.Configuration);

// Register application services as singletons (shared state with API)
builder.Services.AddSingleton<ITeamManagementService, TeamManagementService>();
builder.Services.AddSingleton<IChatQueueService, ChatQueueService>();
builder.Services.AddSingleton<IAgentAssignmentService, AgentAssignmentService>();
builder.Services.AddSingleton<SessionMonitorService>();
builder.Services.AddSingleton<ShiftManagementService>();

// Register the background worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Ensure database is created if using database storage
await host.Services.EnsureDatabaseCreatedAsync();

// Initialize teams on startup
var teamService = host.Services.GetRequiredService<ITeamManagementService>();
await teamService.InitializeTeamsAsync();

// Log the storage type being used
var storageConfig = host.Services.GetRequiredService<StorageConfiguration>();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("PollingMonitor using storage type: {StorageType}", storageConfig.StorageType);

host.Run();

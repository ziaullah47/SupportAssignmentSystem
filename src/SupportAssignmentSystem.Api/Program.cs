using SupportAssignmentSystem.Api;
using SupportAssignmentSystem.Core.Interfaces;
using SupportAssignmentSystem.Core.Services;
using SupportAssignmentSystem.Infrastructure.Extensions;
using SupportAssignmentSystem.Infrastructure.Services;
using SupportAssignmentSystem.Infrastructure.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add Swagger/OpenAPI
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Support Assignment System API",
        Version = "v1",
        Description = "API for managing support chat sessions with automatic agent assignment, queue management, and overflow handling. Supports InMemory, Redis, and Database storage.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Support Assignment System",
            Email = "support@example.com"
        }
    });

    // Add XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Configure storage (InMemory, Redis, or Database) based on appsettings.json
builder.Services.AddStorageServices(builder.Configuration);

// Register application services as singletons
builder.Services.AddSingleton<ITeamManagementService, TeamManagementService>();
builder.Services.AddSingleton<IChatQueueService, ChatQueueService>();
builder.Services.AddSingleton<IAgentAssignmentService, AgentAssignmentService>();
builder.Services.AddSingleton<SessionMonitorService>();
builder.Services.AddSingleton<ShiftManagementService>();

// Add the background monitoring service (combined in same process)
builder.Services.AddHostedService<MonitoringBackgroundService>();

var app = builder.Build();

// Ensure database is created if using database storage
await app.Services.EnsureDatabaseCreatedAsync();

// Initialize teams on startup
var teamService = app.Services.GetRequiredService<ITeamManagementService>();
await teamService.InitializeTeamsAsync();

// Configure the HTTP request pipeline - Enable Swagger in all environments for easy testing
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Support Assignment System API v1");
    options.RoutePrefix = string.Empty; // Set Swagger UI at the app's root (http://localhost:5000/)
    options.DocumentTitle = "Support Assignment System API";
    options.DisplayRequestDuration();
    options.EnableTryItOutByDefault();
});

app.UseHttpsRedirection();
app.MapControllers();

// Log the storage type being used
var storageConfig = app.Services.GetRequiredService<SupportAssignmentSystem.Core.Configuration.StorageConfiguration>();
app.Logger.LogInformation("Using storage type: {StorageType}", storageConfig.StorageType);

app.Run();

// Make Program accessible to tests
public partial class Program { }

using SupportAssignmentSystem.Infrastructure.Services;

namespace SupportAssignmentSystem.Api;

/// <summary>
/// Background service that monitors sessions and manages shifts
/// Runs within the API process to share the same in-memory state
/// </summary>
public class MonitoringBackgroundService : BackgroundService
{
    private readonly ILogger<MonitoringBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public MonitoringBackgroundService(
        ILogger<MonitoringBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Monitoring Background Service started at: {time}", DateTimeOffset.Now);

        // Wait a bit for the application to fully start
        await Task.Delay(1000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a scope to get scoped/singleton services
                using var scope = _serviceProvider.CreateScope();
                
                var sessionMonitor = scope.ServiceProvider.GetRequiredService<SessionMonitorService>();
                var shiftManagement = scope.ServiceProvider.GetRequiredService<ShiftManagementService>();

                // Monitor sessions for inactivity and assignment
                await sessionMonitor.MonitorSessionsAsync(stoppingToken);
                
                // Manage shift transitions
                await shiftManagement.ManageShiftTransitionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in monitoring background service");
            }

            // Monitor every 1 second
            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("Monitoring Background Service stopped at: {time}", DateTimeOffset.Now);
    }
}

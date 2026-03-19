using SupportAssignmentSystem.Infrastructure.Services;

namespace SupportAssignmentSystem.PollingMonitor;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly SessionMonitorService _sessionMonitorService;
    private readonly ShiftManagementService _shiftManagementService;

    public Worker(
        ILogger<Worker> logger, 
        SessionMonitorService sessionMonitorService,
        ShiftManagementService shiftManagementService)
    {
        _logger = logger;
        _sessionMonitorService = sessionMonitorService;
        _shiftManagementService = shiftManagementService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Polling Monitor Worker started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Monitor sessions for inactivity and assignment
                await _sessionMonitorService.MonitorSessionsAsync(stoppingToken);
                
                // Manage shift transitions
                await _shiftManagementService.ManageShiftTransitionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while monitoring sessions or managing shifts");
            }

            // Monitor every 1 second
            await Task.Delay(1000, stoppingToken);
        }

        _logger.LogInformation("Polling Monitor Worker stopped at: {time}", DateTimeOffset.Now);
    }
}

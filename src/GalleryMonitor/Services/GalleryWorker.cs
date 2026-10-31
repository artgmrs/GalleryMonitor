namespace GalleryMonitor.Services;

public sealed class GalleryWorker(IGalleryMonitorService monitorService, ILogger<GalleryWorker> logger, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await monitorService.RunAsync(stoppingToken); }
        catch (Exception exception)
        {
            logger.LogError(exception, "A execução do monitoramento falhou.");
            Environment.ExitCode = 1;
        }
        finally { lifetime.StopApplication(); }
    }
}

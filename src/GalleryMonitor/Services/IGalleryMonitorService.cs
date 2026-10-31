namespace GalleryMonitor.Services;

public interface IGalleryMonitorService
{
    Task RunAsync(CancellationToken cancellationToken);
}

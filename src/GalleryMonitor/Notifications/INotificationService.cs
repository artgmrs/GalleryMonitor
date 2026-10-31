namespace GalleryMonitor.Notifications;

using GalleryMonitor.Models;

public interface INotificationService
{
    Task NotifyAsync(GalleryUpdate update, CancellationToken cancellationToken);
}

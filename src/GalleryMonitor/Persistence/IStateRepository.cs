namespace GalleryMonitor.Persistence;

using GalleryMonitor.Models;

public interface IStateRepository
{
    Task<GalleryState?> GetAsync(string galleryName, CancellationToken cancellationToken);
    Task SaveAsync(string galleryName, GalleryState state, CancellationToken cancellationToken);
}

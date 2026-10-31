namespace GalleryMonitor.Clients;

using GalleryMonitor.Models;

public interface IGalleryClient
{
    Task<GalleryContent> GetContentAsync(Uri galleryUri, CancellationToken cancellationToken);
}

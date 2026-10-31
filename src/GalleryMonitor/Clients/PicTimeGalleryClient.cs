namespace GalleryMonitor.Clients;

using GalleryMonitor.Models;
using GalleryMonitor.Parsers;

/// <summary>Baixa a página pública da galeria para descobrir onde está o <c>gallery.json.txt</c> no CDN e então lê o conteúdo.</summary>
public sealed class PicTimeGalleryClient(HttpClient httpClient, ILogger<PicTimeGalleryClient> logger) : IGalleryClient
{
    public async Task<GalleryContent> GetContentAsync(Uri galleryUri, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Consultando galeria {GalleryUrl}", galleryUri);
        var page = await httpClient.GetStringAsync(galleryUri, cancellationToken);
        var source = PicTimeParser.ParsePage(page);
        var json = await httpClient.GetStringAsync(source.GalleryDataUri, cancellationToken);
        var content = PicTimeParser.ParseGallery(json, source.MediaBaseUri);
        logger.LogInformation("Galeria lida em {ElapsedMilliseconds} ms: {SceneCount} cenas, {PhotoCount} fotos", stopwatch.ElapsedMilliseconds, content.Scenes.Count, content.Scenes.Sum(static scene => scene.PhotoIds.Count));
        return content;
    }
}

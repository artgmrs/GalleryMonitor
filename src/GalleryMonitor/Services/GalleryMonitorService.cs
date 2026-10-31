namespace GalleryMonitor.Services;

using GalleryMonitor.Clients;
using GalleryMonitor.Configuration;
using GalleryMonitor.Models;
using GalleryMonitor.Notifications;
using GalleryMonitor.Persistence;

public sealed class GalleryMonitorService(IGalleryClient galleryClient, IStateRepository stateRepository, IEnumerable<INotificationService> notificationServices, IOptions<GalleryOptions> options, ILogger<GalleryMonitorService> logger) : IGalleryMonitorService
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Iniciando monitoramento de {GalleryCount} galerias", options.Value.Items.Count);
        foreach (var item in options.Value.Items)
        {
            await MonitorGalleryAsync(item, cancellationToken);
        }
        logger.LogInformation("Monitoramento concluído em {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
    }

    private async Task MonitorGalleryAsync(GalleryItemOptions item, CancellationToken cancellationToken)
    {
        var galleryUri = new Uri(item.Url, UriKind.Absolute);
        var content = await galleryClient.GetContentAsync(galleryUri, cancellationToken);
        var previousState = await stateRepository.GetAsync(item.Name, cancellationToken);
        if (previousState is null)
        {
            await stateRepository.SaveAsync(item.Name, Merge(new GalleryState(), content), cancellationToken);
            logger.LogInformation("Primeira execução para {GalleryName}; estado salvo sem notificações", item.Name);
            return;
        }

        var update = FindUpdate(item.Name, galleryUri, previousState, content);
        if (update is null)
        {
            logger.LogInformation("Nenhuma novidade em {GalleryName}", item.Name);
            return;
        }

        logger.LogInformation("{GalleryName}: {NewPhotoCount} fotos e {NewVideoCount} vídeos novos", item.Name, update.NewPhotoCount, update.NewVideoCount);
        // Notifica antes de persistir: se o envio falhar, o estado não avança e a próxima execução tenta de novo.
        foreach (var notificationService in notificationServices)
        {
            await notificationService.NotifyAsync(update, cancellationToken);
        }
        await stateRepository.SaveAsync(item.Name, Merge(previousState, content), cancellationToken);
    }

    private static GalleryUpdate? FindUpdate(string galleryName, Uri galleryUri, GalleryState previousState, GalleryContent content)
    {
        var knownPhotos = previousState.PhotoIds.ToHashSet();
        var knownVideos = previousState.VideoIds.ToHashSet(StringComparer.Ordinal);
        var scenes = content.Scenes
            .Select(scene => new SceneUpdate(scene.Name, scene.PhotoIds.Where(id => !knownPhotos.Contains(id)).Distinct().ToList(), scene.Videos.Where(video => !knownVideos.Contains(video.Id)).ToList()))
            .Where(static scene => scene.NewPhotoIds.Count > 0 || scene.NewVideos.Count > 0)
            .ToList();
        if (scenes.Count == 0) return null;

        var previewPhotoId = scenes.SelectMany(static scene => scene.NewPhotoIds).Select(static id => (long?)id).FirstOrDefault();
        return new(galleryName, galleryUri, scenes, previewPhotoId is { } photoId ? content.GetPhotoPreviewUri(photoId) : null);
    }

    private static GalleryState Merge(GalleryState state, GalleryContent content) => new()
    {
        PhotoIds = state.PhotoIds.Concat(content.Scenes.SelectMany(static scene => scene.PhotoIds)).Distinct().Order().ToList(),
        VideoIds = state.VideoIds.Concat(content.Scenes.SelectMany(static scene => scene.Videos).Select(static video => video.Id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList()
    };
}

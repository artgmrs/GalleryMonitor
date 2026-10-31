namespace GalleryMonitor.Models;

public sealed record GalleryUpdate(string GalleryName, Uri GalleryUri, IReadOnlyList<SceneUpdate> Scenes, Uri? PreviewImageUri)
{
    public int NewPhotoCount => Scenes.Sum(static scene => scene.NewPhotoIds.Count);
    public int NewVideoCount => Scenes.Sum(static scene => scene.NewVideos.Count);
}

public sealed record SceneUpdate(string Name, IReadOnlyList<long> NewPhotoIds, IReadOnlyList<GalleryVideo> NewVideos);

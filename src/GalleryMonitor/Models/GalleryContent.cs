namespace GalleryMonitor.Models;

/// <summary>Conteúdo público da galeria no momento da consulta. <see cref="MediaBaseUri"/> é a pasta do projeto no CDN.</summary>
public sealed record GalleryContent(IReadOnlyList<GalleryScene> Scenes, Uri MediaBaseUri)
{
    public Uri GetPhotoPreviewUri(long photoId) => new(MediaBaseUri, $"lowres/{photoId}.jpg");
}

public sealed record GalleryScene(long Id, string Name, IReadOnlyList<long> PhotoIds, IReadOnlyList<GalleryVideo> Videos);

public sealed record GalleryVideo(string Id, string Title);

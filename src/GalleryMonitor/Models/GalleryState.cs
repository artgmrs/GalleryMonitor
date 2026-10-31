namespace GalleryMonitor.Models;

/// <summary>Tudo o que já foi visto na galeria. Só cresce: fotos ocultadas e republicadas não geram nova notificação.</summary>
public sealed record GalleryState
{
    public IReadOnlyList<long> PhotoIds { get; init; } = [];
    public IReadOnlyList<string> VideoIds { get; init; } = [];
}

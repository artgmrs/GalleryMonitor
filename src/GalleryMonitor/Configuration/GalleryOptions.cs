namespace GalleryMonitor.Configuration;

public sealed class GalleryOptions
{
    public const string SectionName = "Gallery";
    public List<GalleryItemOptions> Items { get; init; } = [];
    public string StateFilePath { get; init; } = "data/gallery-state.json";

    public bool IsValid() =>
        Items.Count > 0
        && Items.All(static item => !string.IsNullOrWhiteSpace(item.Name) && Uri.TryCreate(item.Url, UriKind.Absolute, out _))
        && Items.Select(static item => item.Name).Distinct(StringComparer.Ordinal).Count() == Items.Count;
}

public sealed class GalleryItemOptions
{
    public string Name { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
}

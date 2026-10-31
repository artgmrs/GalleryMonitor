namespace GalleryMonitor.Configuration;

public sealed class DiscordOptions
{
    public const string SectionName = "Notifications:Discord";
    public string WebhookUrl { get; set; } = string.Empty;
    public int MaxRetries { get; init; } = 3;
}

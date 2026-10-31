namespace GalleryMonitor.Persistence;

using GalleryMonitor.Configuration;
using GalleryMonitor.Models;

public sealed class JsonStateRepository(IOptions<GalleryOptions> options, ILogger<JsonStateRepository> logger) : IStateRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath = Path.GetFullPath(options.Value.StateFilePath);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<GalleryState?> GetAsync(string galleryName, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try { return (await ReadAllAsync(cancellationToken)).GetValueOrDefault(galleryName); }
        finally { _lock.Release(); }
    }

    public async Task SaveAsync(string galleryName, GalleryState state, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var states = await ReadAllAsync(cancellationToken);
            states[galleryName] = state;
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var temporaryPath = _filePath + ".tmp";
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(stream, states, JsonOptions, cancellationToken);
            }
            File.Move(temporaryPath, _filePath, true);
            logger.LogInformation("Estado persistido em {StateFilePath}", _filePath);
        }
        finally { _lock.Release(); }
    }

    private async Task<Dictionary<string, GalleryState>> ReadAllAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath)) return new(StringComparer.Ordinal);
        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<Dictionary<string, GalleryState>>(stream, cancellationToken: cancellationToken) ?? new(StringComparer.Ordinal);
    }
}

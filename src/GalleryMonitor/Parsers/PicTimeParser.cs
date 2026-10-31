namespace GalleryMonitor.Parsers;

using GalleryMonitor.Exceptions;
using GalleryMonitor.Models;

public sealed record PicTimeGallerySource(Uri MediaBaseUri, Uri GalleryDataUri);

/// <summary>
/// Formato reproduzido do frontend da Pic-Time:
/// a página declara <c>initParams</c> (projeto, token e timestamp) e <c>_pictimeStorageMapping</c> (domínios por storage);
/// o projeto fica em <c>/pictures/{id/1e6}/{id/1e3 % 1000}/{id}/{pathToken}/</c>;
/// em <c>gallery.json.txt</c> versão 3+, cada foto ocupa 5 posições em <c>scenes[].photos</c> e a primeira é o deslocamento em relação a <c>minId</c>.
/// </summary>
public static partial class PicTimeParser
{
    private const int PhotoStride = 5;
    private const int FeaturedVideoEmbeddingType = 80;

    public static PicTimeGallerySource ParsePage(string html)
    {
        try
        {
            using var initParams = ReadAssignedJson(html, InitParamsRegex(), "initParams");
            using var storageMapping = ReadAssignedJson(html, StorageMappingRegex(), "_pictimeStorageMapping");
            var root = initParams.RootElement;
            var projectId = root.GetProperty("projectId").GetInt64();
            var storageId = root.GetProperty("projectStorageId").GetInt32();
            var pathToken = root.GetProperty("projectPathToken").GetString();
            var timestamp = root.GetProperty("galleryTimeStamp").GetString();
            var noCdn = root.TryGetProperty("noCDN", out var noCdnElement) && noCdnElement.ValueKind == JsonValueKind.True;
            if (string.IsNullOrWhiteSpace(pathToken) || string.IsNullOrWhiteSpace(timestamp)) throw new GalleryParserException("initParams não trouxe projectPathToken ou galleryTimeStamp; a galeria pode estar privada ou protegida por senha.");

            var storage = storageMapping.RootElement.EnumerateArray().FirstOrDefault(item => item.GetProperty("storageId").GetInt32() == storageId);
            if (storage.ValueKind != JsonValueKind.Object) throw new GalleryParserException($"Storage {storageId} não encontrado em _pictimeStorageMapping.");
            var domain = storage.GetProperty(noCdn ? "dataDomain" : "cdnDomain").GetString()?.TrimEnd('/');

            var mediaBaseUri = new Uri($"{domain}/pictures/{projectId / 1_000_000}/{projectId / 1_000 % 1_000}/{projectId}/{pathToken}/", UriKind.Absolute);
            return new(mediaBaseUri, new Uri(mediaBaseUri, $"gallery.json.txt?ts={Uri.EscapeDataString(timestamp)}"));
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or JsonException or UriFormatException)
        {
            throw new GalleryParserException("A página da Pic-Time não tem o formato esperado.", exception);
        }
    }

    public static GalleryContent ParseGallery(string json, Uri mediaBaseUri)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var version = root.GetProperty("version").GetInt32();
            if (version is < 3 or > 5) throw new GalleryParserException($"Versão {version} do gallery.json não suportada.");

            var minId = root.GetProperty("minId").GetInt64();
            var restricted = root.TryGetProperty("rstPhotos", out var restrictedElement) && restrictedElement.ValueKind == JsonValueKind.Array
                ? restrictedElement.EnumerateArray().Select(offset => minId + offset.GetInt64()).ToHashSet()
                : [];
            var scenes = root.GetProperty("scenes").EnumerateArray().Select(scene => new GalleryScene(
                scene.GetProperty("id").GetInt64(),
                scene.TryGetProperty("nm", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                ReadPhotoIds(scene.GetProperty("photos"), minId, restricted),
                ReadVideos(scene))).ToList();
            return new(scenes, mediaBaseUri);
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException or JsonException)
        {
            throw new GalleryParserException("O gallery.json da Pic-Time não tem o formato esperado.", exception);
        }
    }

    private static List<long> ReadPhotoIds(JsonElement photos, long minId, HashSet<long> restricted)
    {
        var length = photos.GetArrayLength();
        if (length % PhotoStride != 0) throw new GalleryParserException($"scenes[].photos tem {length} posições, que não é múltiplo de {PhotoStride}.");
        var ids = new List<long>(length / PhotoStride);
        for (var index = 0; index < length; index += PhotoStride)
        {
            var id = minId + photos[index].GetInt64();
            if (!restricted.Contains(id)) ids.Add(id);
        }
        return ids;
    }

    private static List<GalleryVideo> ReadVideos(JsonElement scene)
    {
        if (!scene.TryGetProperty("embedding", out var embedding) || embedding.ValueKind != JsonValueKind.Array) return [];
        return embedding.EnumerateArray()
            .Where(static item => item.GetProperty("type").GetInt32() == FeaturedVideoEmbeddingType && !(item.TryGetProperty("hidden", out var hidden) && hidden.ValueKind == JsonValueKind.True))
            .Select(static item => new GalleryVideo(item.GetProperty("id").GetString()!, item.TryGetProperty("description", out var description) && !string.IsNullOrWhiteSpace(description.GetString()) ? description.GetString()! : "Vídeo"))
            .ToList();
    }

    private static JsonDocument ReadAssignedJson(string html, Regex assignment, string variableName)
    {
        var match = assignment.Match(html);
        if (!match.Success) throw new GalleryParserException($"A página não declara {variableName}.");
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(html[(match.Index + match.Length)..]));
        return JsonDocument.ParseValue(ref reader);
    }

    [GeneratedRegex(@"\binitParams\s*=\s*(?=\{)")]
    private static partial Regex InitParamsRegex();

    [GeneratedRegex(@"\b_pictimeStorageMapping\s*=\s*(?=\[)")]
    private static partial Regex StorageMappingRegex();
}

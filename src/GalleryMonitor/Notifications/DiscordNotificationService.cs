namespace GalleryMonitor.Notifications;

using GalleryMonitor.Configuration;
using GalleryMonitor.Models;

/// <summary>Publica uma única mensagem por galeria e execução, agrupada por cena, para não gerar centenas de mensagens quando um lote de fotos é publicado.</summary>
public sealed class DiscordNotificationService(HttpClient httpClient, IOptions<DiscordOptions> options, ILogger<DiscordNotificationService> logger) : INotificationService
{
    private const int MaxSceneFields = 24;
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public async Task NotifyAsync(GalleryUpdate update, CancellationToken cancellationToken)
    {
        // Falha em vez de ignorar: o estado só avança depois da notificação, então as novidades ficam para a próxima execução.
        if (string.IsNullOrWhiteSpace(options.Value.WebhookUrl)) throw new InvalidOperationException("Há novidades, mas o webhook do Discord não está configurado (DISCORD_WEBHOOK_URL).");

        var payload = BuildPayload(update);
        for (var attempt = 0; ; attempt++)
        {
            using var response = await httpClient.PostAsJsonAsync(options.Value.WebhookUrl, payload, JsonOptions, cancellationToken);
            if (response.StatusCode != HttpStatusCode.TooManyRequests)
            {
                response.EnsureSuccessStatusCode();
                logger.LogInformation("Notificação enviada para {GalleryName}", update.GalleryName);
                return;
            }

            if (attempt >= options.Value.MaxRetries) throw new HttpRequestException("Discord excedeu o limite de requisições após as tentativas configuradas.", null, response.StatusCode);
            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
            logger.LogWarning("Discord limitou a requisição; nova tentativa em {Delay}", delay);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static object BuildPayload(GalleryUpdate update)
    {
        var fields = update.Scenes.Take(MaxSceneFields).Select(static scene => new
        {
            name = Truncate(string.IsNullOrWhiteSpace(scene.Name) ? "Sem nome" : scene.Name, 256),
            value = Truncate(DescribeScene(scene), 1024),
            inline = false
        }).ToList();
        if (update.Scenes.Count > MaxSceneFields)
        {
            fields.Add(new { name = "…", value = $"e mais {update.Scenes.Count - MaxSceneFields} cenas", inline = false });
        }

        var embed = new
        {
            title = "📸 Novidades na galeria",
            description = $"**{update.GalleryName}** recebeu {DescribeTotals(update.NewPhotoCount, update.NewVideoCount)}.",
            url = update.GalleryUri.ToString(),
            color = 0xC9A227,
            fields,
            image = update.PreviewImageUri is null ? null : new { url = update.PreviewImageUri.ToString() },
            footer = new { text = "Gallery Monitor • GitHub Actions" }
        };
        return new { embeds = new[] { embed } };
    }

    private static string DescribeScene(SceneUpdate scene)
    {
        var lines = new List<string>();
        if (scene.NewPhotoIds.Count > 0) lines.Add($"+{Plural(scene.NewPhotoIds.Count, "foto", "fotos")}");
        lines.AddRange(scene.NewVideos.Select(static video => $"🎬 {video.Title}"));
        return string.Join('\n', lines);
    }

    private static string DescribeTotals(int photos, int videos)
    {
        var parts = new List<string>(2);
        if (photos > 0) parts.Add(Plural(photos, "foto nova", "fotos novas"));
        if (videos > 0) parts.Add(Plural(videos, "vídeo novo", "vídeos novos"));
        return string.Join(" e ", parts);
    }

    private static string Plural(int count, string singular, string plural) => $"{count} {(count == 1 ? singular : plural)}";
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}

using System.Net.Http.Headers;
using FluentAssertions;
using GalleryMonitor.Configuration;
using GalleryMonitor.Models;
using GalleryMonitor.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GalleryMonitor.Tests;

public sealed class DiscordNotificationServiceTests
{
    private static readonly GalleryUpdate Update = new("G", new Uri("https://example.com/g"), [new SceneUpdate("A", [1, 2], [])], null);

    private static DiscordNotificationService CreateService(StubHttpMessageHandler handler, string webhook = "https://discord.test/hook", int maxRetries = 3) =>
        new(new HttpClient(handler), Options.Create(new DiscordOptions { WebhookUrl = webhook, MaxRetries = maxRetries }), NullLogger<DiscordNotificationService>.Instance);

    [Fact]
    public async Task NotifyAsync_RateLimitedThenSuccess_RetriesOnce()
    {
        StubHttpMessageHandler? handler = null;
        handler = new StubHttpMessageHandler(_ => handler!.Calls == 1 ? RateLimited() : new HttpResponseMessage(HttpStatusCode.NoContent));
        await CreateService(handler).NotifyAsync(Update, CancellationToken.None);
        handler.Calls.Should().Be(2);
    }

    [Fact]
    public async Task NotifyAsync_ServerError_Throws()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var act = () => CreateService(handler).NotifyAsync(Update, CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task NotifyAsync_EmptyWebhook_ThrowsWithoutCallingHttp()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var act = () => CreateService(handler, webhook: "").NotifyAsync(Update, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(0);
    }

    private static HttpResponseMessage RateLimited()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
        return response;
    }
}

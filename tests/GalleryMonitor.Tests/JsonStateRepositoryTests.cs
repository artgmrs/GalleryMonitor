using FluentAssertions;
using GalleryMonitor.Configuration;
using GalleryMonitor.Models;
using GalleryMonitor.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GalleryMonitor.Tests;

public sealed class JsonStateRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gallery-monitor-tests-" + Guid.NewGuid().ToString("N"));

    private JsonStateRepository CreateRepository() =>
        new(Options.Create(new GalleryOptions { StateFilePath = Path.Combine(_directory, "sub", "state.json") }), NullLogger<JsonStateRepository>.Instance);

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsStatePerGallery()
    {
        var repository = CreateRepository();
        await repository.SaveAsync("A", new GalleryState { PhotoIds = [1, 2], VideoIds = ["v"] }, CancellationToken.None);
        await repository.SaveAsync("B", new GalleryState { PhotoIds = [9] }, CancellationToken.None);

        var state = await CreateRepository().GetAsync("A", CancellationToken.None);

        state!.PhotoIds.Should().Equal(1, 2);
        state.VideoIds.Should().Equal("v");
        (await repository.GetAsync("B", CancellationToken.None))!.PhotoIds.Should().Equal(9);
    }

    [Fact]
    public async Task GetAsync_UnknownGallery_ReturnsNull()
    {
        (await CreateRepository().GetAsync("X", CancellationToken.None)).Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}

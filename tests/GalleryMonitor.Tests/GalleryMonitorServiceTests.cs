using FluentAssertions;
using GalleryMonitor.Clients;
using GalleryMonitor.Configuration;
using GalleryMonitor.Models;
using GalleryMonitor.Notifications;
using GalleryMonitor.Persistence;
using GalleryMonitor.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GalleryMonitor.Tests;

public sealed class GalleryMonitorServiceTests
{
    private static readonly Uri GalleryUri = new("https://example.com/gallery");
    private static readonly Uri MediaBase = new("https://cdn.example/pictures/1/2/3/tok/");

    private readonly Mock<IGalleryClient> _client = new();
    private readonly Mock<IStateRepository> _repository = new();
    private readonly Mock<INotificationService> _notifier = new();

    private GalleryMonitorService CreateService()
    {
        var options = Options.Create(new GalleryOptions { Items = [new GalleryItemOptions { Name = "G", Url = GalleryUri.ToString() }] });
        return new(_client.Object, _repository.Object, [_notifier.Object], options, NullLogger<GalleryMonitorService>.Instance);
    }

    private void SetupContent(params GalleryScene[] scenes) => _client.Setup(c => c.GetContentAsync(GalleryUri, It.IsAny<CancellationToken>())).ReturnsAsync(new GalleryContent(scenes, MediaBase));
    private void SetupState(GalleryState? state) => _repository.Setup(r => r.GetAsync("G", It.IsAny<CancellationToken>())).ReturnsAsync(state);

    [Fact]
    public async Task RunAsync_FirstRun_SavesStateWithoutNotifying()
    {
        SetupContent(new GalleryScene(1, "A", [3, 1], [new GalleryVideo("v", "V")]));
        SetupState(null);
        await CreateService().RunAsync(CancellationToken.None);
        _notifier.Verify(n => n.NotifyAsync(It.IsAny<GalleryUpdate>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.SaveAsync("G", It.Is<GalleryState>(s => s.PhotoIds.SequenceEqual(new long[] { 1, 3 }) && s.VideoIds.SequenceEqual(new[] { "v" })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_NewPhotosInExistingAndNewScene_NotifiesOnceGroupedAndSavesSortedUnion()
    {
        SetupContent(new GalleryScene(1, "A", [1, 2, 10], []), new GalleryScene(2, "B", [20, 5], []));
        SetupState(new GalleryState { PhotoIds = [1, 2] });
        GalleryUpdate? sent = null;
        _notifier.Setup(n => n.NotifyAsync(It.IsAny<GalleryUpdate>(), It.IsAny<CancellationToken>())).Callback<GalleryUpdate, CancellationToken>((u, _) => sent = u).Returns(Task.CompletedTask);
        GalleryState? saved = null;
        _repository.Setup(r => r.SaveAsync("G", It.IsAny<GalleryState>(), It.IsAny<CancellationToken>())).Callback<string, GalleryState, CancellationToken>((_, s, _) => saved = s).Returns(Task.CompletedTask);

        await CreateService().RunAsync(CancellationToken.None);

        _notifier.Verify(n => n.NotifyAsync(It.IsAny<GalleryUpdate>(), It.IsAny<CancellationToken>()), Times.Once);
        sent!.Scenes.Select(s => (s.Name, string.Join(",", s.NewPhotoIds))).Should().Equal(("A", "10"), ("B", "20,5"));
        sent.PreviewImageUri.Should().Be(new Uri(MediaBase, "lowres/10.jpg"));
        saved!.PhotoIds.Should().Equal(1, 2, 5, 10, 20);
    }

    [Fact]
    public async Task RunAsync_NoNovelty_NeitherNotifiesNorSaves()
    {
        SetupContent(new GalleryScene(1, "A", [1, 2], []));
        SetupState(new GalleryState { PhotoIds = [1, 2] });
        await CreateService().RunAsync(CancellationToken.None);
        _notifier.Verify(n => n.NotifyAsync(It.IsAny<GalleryUpdate>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<GalleryState>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_PhotoSeenThenHiddenAndRepublished_DoesNotNotify()
    {
        // Estado já contém a foto 2 (vista antes de ser ocultada); ela volta a aparecer na galeria.
        SetupContent(new GalleryScene(1, "A", [1, 2], []));
        SetupState(new GalleryState { PhotoIds = [1, 2, 3] });
        await CreateService().RunAsync(CancellationToken.None);
        _notifier.Verify(n => n.NotifyAsync(It.IsAny<GalleryUpdate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_NotifierThrows_DoesNotSaveState()
    {
        SetupContent(new GalleryScene(1, "A", [1, 2], []));
        SetupState(new GalleryState { PhotoIds = [1] });
        _notifier.Setup(n => n.NotifyAsync(It.IsAny<GalleryUpdate>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("falhou"));
        var act = () => CreateService().RunAsync(CancellationToken.None);
        await act.Should().ThrowAsync<HttpRequestException>();
        _repository.Verify(r => r.SaveAsync(It.IsAny<string>(), It.IsAny<GalleryState>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

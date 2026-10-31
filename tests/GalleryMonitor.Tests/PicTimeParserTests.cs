using FluentAssertions;
using GalleryMonitor.Exceptions;
using GalleryMonitor.Parsers;

namespace GalleryMonitor.Tests;

public sealed class PicTimeParserTests
{
    private static string Page(bool noCdn = false) => $$"""
        <html><script>
        var initParams = {"projectId":53962970,"projectStorageId":18,"projectPathToken":"tok123","galleryTimeStamp":"1343","noCDN":{{(noCdn ? "true" : "false")}}};
        var _pictimeStorageMapping = [{"storageId":1,"cdnDomain":"https://cdn1.example","dataDomain":"https://data1.example"},{"storageId":18,"cdnDomain":"https://cdn18.example/","dataDomain":"https://data18.example"}];
        </script></html>
        """;

    private static readonly Uri MediaBase = new("https://cdn.example/pictures/1/2/3/tok/");

    [Fact]
    public void ParsePage_BuildsMediaAndDataUrisFromMatchingStorage()
    {
        var source = PicTimeParser.ParsePage(Page());
        source.MediaBaseUri.ToString().Should().Be("https://cdn18.example/pictures/53/962/53962970/tok123/");
        source.GalleryDataUri.ToString().Should().Be("https://cdn18.example/pictures/53/962/53962970/tok123/gallery.json.txt?ts=1343");
    }

    [Fact]
    public void ParsePage_NoCdn_UsesDataDomain()
    {
        PicTimeParser.ParsePage(Page(noCdn: true)).MediaBaseUri.Host.Should().Be("data18.example");
    }

    [Fact]
    public void ParsePage_WithoutInitParams_Throws()
    {
        var act = () => PicTimeParser.ParsePage("<html><script>var _pictimeStorageMapping = [];</script></html>");
        act.Should().Throw<GalleryParserException>();
    }

    [Fact]
    public void ParseGallery_DecodesPhotoIdsFromEveryFifthElement()
    {
        const string json = """{"version":4,"minId":1000,"scenes":[{"id":1,"nm":"A","photos":[0,9,9,9,9, 5,9,9,9,9, 20,9,9,9,9]}]}""";
        var content = PicTimeParser.ParseGallery(json, MediaBase);
        content.Scenes.Should().ContainSingle().Which.PhotoIds.Should().Equal(1000, 1005, 1020);
        content.Scenes[0].Name.Should().Be("A");
        content.MediaBaseUri.Should().Be(MediaBase);
    }

    [Fact]
    public void ParseGallery_ExcludesRestrictedPhotos()
    {
        const string json = """{"version":3,"minId":1000,"rstPhotos":[5],"scenes":[{"id":1,"nm":"A","photos":[0,0,0,0,0, 5,0,0,0,0, 7,0,0,0,0]}]}""";
        PicTimeParser.ParseGallery(json, MediaBase).Scenes[0].PhotoIds.Should().Equal(1000, 1007);
    }

    [Fact]
    public void ParseGallery_ReturnsOnlyVisibleType80EmbeddingsAsVideos()
    {
        const string json = """
            {"version":5,"minId":0,"scenes":[{"id":1,"nm":"T","photos":[],"embedding":[
              {"type":80,"id":"v1","description":"Trailer"},
              {"type":80,"id":"v2","hidden":true},
              {"type":80,"id":"v3"},
              {"type":10,"id":"x1"}]}]}
            """;
        var videos = PicTimeParser.ParseGallery(json, MediaBase).Scenes[0].Videos;
        videos.Select(v => (v.Id, v.Title)).Should().Equal(("v1", "Trailer"), ("v3", "Vídeo"));
    }

    [Fact]
    public void ParseGallery_UnsupportedVersion_Throws()
    {
        var act = () => PicTimeParser.ParseGallery("""{"version":2,"minId":0,"scenes":[]}""", MediaBase);
        act.Should().Throw<GalleryParserException>();
    }

    [Fact]
    public void ParseGallery_PhotosLengthNotMultipleOfFive_Throws()
    {
        var act = () => PicTimeParser.ParseGallery("""{"version":4,"minId":0,"scenes":[{"id":1,"photos":[1,2,3]}]}""", MediaBase);
        act.Should().Throw<GalleryParserException>();
    }
}

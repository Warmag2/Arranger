using System;
using System.Buffers.Binary;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using Faemiyah.Arranger.Contracts;
using Faemiyah.Arranger.Contracts.Arrangements;
using Faemiyah.Arranger.Contracts.Users;
using NUnit.Framework;

namespace Faemiyah.Arranger.IntegrationTests;

[TestFixture]
public sealed class UserEndpointsTests
{
    private ArrangerApp _app = null!;

    [OneTimeSetUp]
    public void StartServer()
    {
        _app = new ArrangerApp();
    }

    [OneTimeTearDown]
    public async Task StopServerAsync()
    {
        await _app.DisposeAsync();
    }

    [Test]
    public async Task GetPicture_ReturnsBytesWithImmutableCaching()
    {
        using var http = await SignInAsync();
        var picture = Png(16, 16);
        var settings = await ArrangerApp.ReadAsync<UserSettings>(await PutPictureAsync(http, picture));

        var response = await http.GetAsync(ApiRoutes.UserPicture(settings.UserId, settings.PictureVersion!.Value));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(picture);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProfilePictureRules.Png);
        var cacheControl = response.Headers.CacheControl!;
        cacheControl.Private.Should().BeTrue();
        cacheControl.MaxAge.Should().Be(TimeSpan.FromDays(365));
        cacheControl.Extensions.Should().ContainSingle(e => e.Name == "immutable");
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Test]
    public async Task GetPicture_WithoutPicture_IsNotFound()
    {
        using var http = await SignInAsync();
        var settings = await ArrangerApp.ReadAsync<UserSettings>(await http.GetAsync(ApiRoutes.MySettings));

        var response = await http.GetAsync(ApiRoutes.UserPicture(settings.UserId, 1));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetSettings_WithoutRequestedWithHeader_Succeeds()
    {
        using var http = await SignInAsync();
        http.DefaultRequestHeaders.Remove(ApiHeaders.RequestedWith);

        var response = await http.GetAsync(ApiRoutes.MySettings);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task SetPicture_NotAnImage_IsUnsupportedMediaType()
    {
        using var http = await SignInAsync();

        var response = await PutPictureAsync(http, "<svg></svg>"u8.ToArray());

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
    }

    [Test]
    public async Task SetPicture_ShowsInArrangements()
    {
        using var http = await SignInAsync();
        var arrangementId = await CreateArrangementAsync(http);

        var settings = await ArrangerApp.ReadAsync<UserSettings>(await PutPictureAsync(http, Png(16, 16)));

        settings.PictureVersion.Should().NotBeNull();
        (await GetMemberAsync(http, arrangementId)).PictureVersion.Should().Be(settings.PictureVersion);
    }

    [TestCase(16, 16, ProfilePictureRules.MaxBytes)]
    [TestCase(ProfilePictureRules.MaxDimension + 1, 16, 0)]
    [TestCase(16, ProfilePictureRules.MaxDimension + 1, 0)]
    public async Task SetPicture_TooLarge_IsPayloadTooLarge(int width, int height, int padding)
    {
        using var http = await SignInAsync();

        var response = await PutPictureAsync(http, Png(width, height, padding));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Test]
    public async Task UpdateSettings_Rename_ShowsInArrangements()
    {
        using var http = await SignInAsync();
        var arrangementId = await CreateArrangementAsync(http);
        var settings = await ArrangerApp.ReadAsync<UserSettings>(await http.GetAsync(ApiRoutes.MySettings));
        var request = new UpdateUserSettingsRequest(NewUserName(), settings.TimeZoneId, settings.DefaultCurrency);

        var response = await http.PutAsJsonAsync(ApiRoutes.MySettings, request, ArrangerApp.JsonOptions);

        (await ArrangerApp.ReadAsync<UserSettings>(response)).UserName.Should().Be(request.UserName);
        (await GetMemberAsync(http, arrangementId)).UserName.Should().Be(request.UserName);
    }

    [Test]
    public async Task UpdateSettings_WithoutRequestedWithHeader_IsBadRequest()
    {
        using var http = await SignInAsync();
        var settings = await ArrangerApp.ReadAsync<UserSettings>(await http.GetAsync(ApiRoutes.MySettings));
        http.DefaultRequestHeaders.Remove(ApiHeaders.RequestedWith);
        var request = new UpdateUserSettingsRequest(NewUserName(), settings.TimeZoneId, settings.DefaultCurrency);

        var response = await http.PutAsJsonAsync(ApiRoutes.MySettings, request, ArrangerApp.JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ArrangerApp.ReadAsync<UserSettings>(await http.GetAsync(ApiRoutes.MySettings))).UserName.Should().Be(settings.UserName);
    }

    private static async Task<Guid> CreateArrangementAsync(HttpClient http)
    {
        var response = await http.PostAsJsonAsync(ApiRoutes.Arrangements, new CreateArrangementRequest("User test", string.Empty), ArrangerApp.JsonOptions);
        return (await ArrangerApp.ReadAsync<ArrangementSnapshot>(response)).Id;
    }

    private static async Task<ArrangementMemberSnapshot> GetMemberAsync(HttpClient http, Guid arrangementId)
    {
        return (await ArrangerApp.ReadAsync<ArrangementSnapshot>(await http.GetAsync(ApiRoutes.Arrangement(arrangementId)))).Members.Single();
    }

    private static string NewUserName()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static byte[] Png(int width, int height, int padding = 0)
    {
        // The signature and the IHDR header the server reads, then zero padding. The pixels are never decoded.
        var data = new byte[24 + padding];
        ((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D]).CopyTo(data);
        "IHDR"u8.CopyTo(data.AsSpan(12));
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(20), height);
        return data;
    }

    private static async Task<HttpResponseMessage> PutPictureAsync(HttpClient http, byte[] data)
    {
        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(ProfilePictureRules.Png);
        return await http.PutAsync(ApiRoutes.MyPicture, content);
    }

    private async Task<HttpClient> SignInAsync()
    {
        return _app.CreateHttpClient(await _app.RegisterAsync(NewUserName()));
    }
}

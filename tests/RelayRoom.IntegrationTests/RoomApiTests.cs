using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using RelayRoom.Application.Services;
using RelayRoom.Core.Abstractions;
using RelayRoom.Core.Domain;
using RelayRoom.Core.Rules;
using RelayRoom.Persistence;

namespace RelayRoom.IntegrationTests;

public sealed class RoomApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public RoomApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateAndJoinRoom_IssuesDeviceJwt()
    {
        var created = await CreateRoomAsync("Laptop");
        Assert.Equal(6, created.Room.PublicCode.Length);
        Assert.False(string.IsNullOrWhiteSpace(created.AccessToken));

        var joined = await JoinAsync(created.Room.PublicCode, "Phone");
        Assert.Equal(created.Room.Id, joined.Room.Id);
        Assert.NotEqual(created.Device.Id, joined.Device.Id);

        Authorize(_client, joined.AccessToken);
        var detail = await _client.GetFromJsonAsync<RoomDetailDto>($"/api/rooms/{joined.Room.Id}", Json);
        Assert.Equal(2, detail!.Devices.Count);
    }

    [Fact]
    public async Task JoinUnknownCode_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/rooms/join", new { code = "ZZZZZZ" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_WithoutToken_Returns401()
    {
        var created = await CreateRoomAsync();
        var anonymous = _factory.CreateClient();
        var response = await anonymous.GetAsync($"/api/rooms/{created.Room.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetRoom_WithOtherRoomToken_Returns403()
    {
        var first = await CreateRoomAsync();
        var second = await CreateRoomAsync();
        Authorize(_client, first.AccessToken);
        var response = await _client.GetAsync($"/api/rooms/{second.Room.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task JoinFullRoom_Returns409()
    {
        var host = await CreateRoomAsync();
        for (var i = 0; i < RoomLimits.MaxDevices - 1; i++)
        {
            await JoinAsync(host.Room.PublicCode, $"Guest {i}");
        }

        var response = await _client.PostAsJsonAsync("/api/rooms/join", new { code = host.Room.PublicCode, displayName = "Overflow" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task JoinExpiredRoom_Returns410()
    {
        var host = await CreateRoomAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var room = await db.Rooms.FindAsync(host.Room.Id);
        Assert.NotNull(room);
        room!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var response = await _client.PostAsJsonAsync("/api/rooms/join", new { code = host.Room.PublicCode, displayName = "Late" });
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task RoomQuota_Returns413()
    {
        var host = await CreateRoomAsync();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var room = await db.Rooms.FindAsync(host.Room.Id);
        Assert.NotNull(room);
        room!.UsedBytes = room.MaxBytes - 10;
        await db.SaveChangesAsync();

        Authorize(_client, host.AccessToken);
        var response = await _client.PostAsJsonAsync($"/api/rooms/{host.Room.Id}/transfers", new
        {
            kind = "File",
            fileName = "over-quota.txt",
            mimeType = "text/plain",
            sizeBytes = 11
        });
        Assert.Equal((HttpStatusCode)413, response.StatusCode);
    }

    [Fact]
    public async Task OversizedFileIntent_Returns413()
    {
        var host = await CreateRoomAsync();
        Authorize(_client, host.AccessToken);
        var response = await _client.PostAsJsonAsync($"/api/rooms/{host.Room.Id}/transfers", new
        {
            kind = "File",
            fileName = "huge.bin",
            mimeType = "application/octet-stream",
            sizeBytes = RoomLimits.MaxFileBytes + 1
        });
        Assert.Equal((HttpStatusCode)413, response.StatusCode);
    }

    [Fact]
    public async Task DisallowedMime_Returns400()
    {
        var host = await CreateRoomAsync();
        Authorize(_client, host.AccessToken);
        var response = await _client.PostAsJsonAsync($"/api/rooms/{host.Room.Id}/transfers", new
        {
            kind = "File",
            fileName = "payload.exe",
            mimeType = "application/x-msdownload",
            sizeBytes = 12
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TextTransfer_LandsInInbox()
    {
        var host = await CreateRoomAsync();
        var guest = await JoinAsync(host.Room.PublicCode);

        Authorize(_client, host.AccessToken);
        var create = await _client.PostAsJsonAsync($"/api/rooms/{host.Room.Id}/transfers", new
        {
            kind = "Text",
            textBody = "hello from laptop"
        });
        create.EnsureSuccessStatusCode();

        Authorize(_client, guest.AccessToken);
        var list = await _client.GetFromJsonAsync<TransferDto[]>($"/api/rooms/{host.Room.Id}/transfers", Json);
        Assert.Contains(list!, t => t.TextBody == "hello from laptop" && t.TargetDeviceId is null);
    }

    [Fact]
    public async Task TargetedLinkTransfer_CreatesOfferedDelivery()
    {
        var host = await CreateRoomAsync();
        var guest = await JoinAsync(host.Room.PublicCode);

        Authorize(_client, host.AccessToken);
        var create = await _client.PostAsJsonAsync($"/api/rooms/{host.Room.Id}/transfers", new
        {
            kind = "Link",
            textBody = "https://learn.microsoft.com/aspnet/core/signalr/introduction",
            targetDeviceId = guest.Device.Id
        });
        var transfer = await create.Content.ReadFromJsonAsync<TransferDto>(Json);
        Assert.Equal(guest.Device.Id, transfer!.TargetDeviceId);
        Assert.Contains(transfer.Deliveries, d => d.DeviceId == guest.Device.Id && d.Status == DeliveryStatus.Offered);
    }

    [Fact]
    public async Task RenameDevice_UpdatesDisplayName()
    {
        var host = await CreateRoomAsync("Old name");
        Authorize(_client, host.AccessToken);
        var response = await _client.PatchAsJsonAsync("/api/devices/me", new { displayName = "Kitchen laptop" });
        await EnsureSuccessAsync(response);
        var device = await response.Content.ReadFromJsonAsync<DeviceDto>(Json);
        Assert.Equal("Kitchen laptop", device!.DisplayName);
    }

    [Fact]
    public async Task GuestCannotCloseRoom()
    {
        var host = await CreateRoomAsync();
        var guest = await JoinAsync(host.Room.PublicCode);
        Authorize(_client, guest.AccessToken);
        var response = await _client.PostAsync($"/api/rooms/{host.Room.Id}/close", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TusUpload_ThenDownloadContent()
    {
        var host = await CreateRoomAsync();
        Authorize(_client, host.AccessToken);
        var payload = Encoding.UTF8.GetBytes("png-bytes-not-really");
        var intent = await _client.PostAsJsonAsync($"/api/rooms/{host.Room.Id}/transfers", new
        {
            kind = "File",
            fileName = "note.txt",
            mimeType = "text/plain",
            sizeBytes = payload.Length
        });
        await EnsureSuccessAsync(intent);
        var transfer = await intent.Content.ReadFromJsonAsync<TransferDto>(Json);

        var metadata = "transferId " + Convert.ToBase64String(Encoding.UTF8.GetBytes(transfer!.Id.ToString()));
        using var createUpload = new HttpRequestMessage(HttpMethod.Post, "/api/uploads");
        createUpload.Headers.Add("Tus-Resumable", "1.0.0");
        createUpload.Headers.Add("Upload-Length", payload.Length.ToString());
        createUpload.Headers.Add("Upload-Metadata", metadata);
        createUpload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.AccessToken);
        var createdUpload = await _client.SendAsync(createUpload);
        await EnsureSuccessAsync(createdUpload);
        var location = createdUpload.Headers.Location!;

        using var patch = new HttpRequestMessage(HttpMethod.Patch, location);
        patch.Headers.Add("Tus-Resumable", "1.0.0");
        patch.Headers.Add("Upload-Offset", "0");
        patch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.AccessToken);
        patch.Content = new ByteArrayContent(payload);
        patch.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        var patched = await _client.SendAsync(patch);
        await EnsureSuccessAsync(patched);

        var download = await _client.GetFromJsonAsync<DownloadDto>($"/api/transfers/{transfer.Id}/download-url", Json);
        Assert.Contains("/content", download!.Url);
        var content = await _client.GetByteArrayAsync($"/api/transfers/{transfer.Id}/content");
        Assert.Equal(payload, content);
    }

    [Fact]
    public async Task SignalR_NotifiesWhenGuestJoins()
    {
        var host = await CreateRoomAsync();
        var joined = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress!, $"hubs/room?access_token={host.AccessToken}"), options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        connection.On<DeviceDto>("DeviceJoined", device => joined.TrySetResult(device.DisplayName));
        await connection.StartAsync();
        await JoinAsync(host.Room.PublicCode, "Phone");

        var completed = await Task.WhenAny(joined.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(joined.Task, completed);
        Assert.Equal("Phone", await joined.Task);
    }

    [Fact]
    public async Task Cleanup_ExpiresAndPurgesRoomAndBlobs()
    {
        var host = await CreateRoomAsync();
        using var scope = _factory.Services.CreateScope();
        var blobs = scope.ServiceProvider.GetRequiredService<IBlobStorage>();
        var key = $"{host.Room.Id:N}/purge-check";
        await using (var stream = new MemoryStream("stale"u8.ToArray()))
        {
            await blobs.UploadAsync(key, stream, "text/plain");
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var room = await db.Rooms.FindAsync(host.Room.Id);
        Assert.NotNull(room);
        room!.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        var cleanup = scope.ServiceProvider.GetRequiredService<IRoomCleanupService>();
        var result = await cleanup.RunOnceAsync(CancellationToken.None);
        Assert.True(result.Expired >= 1);
        Assert.True(result.Purged >= 1);

        db.ChangeTracker.Clear();
        Assert.Null(await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == host.Room.Id));
        await Assert.ThrowsAsync<FileNotFoundException>(() => blobs.OpenReadAsync(key));
    }

    private async Task<SessionDto> CreateRoomAsync(string? displayName = null)
    {
        var response = await _client.PostAsJsonAsync("/api/rooms", new { displayName });
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<SessionDto>(Json))!;
    }

    private async Task<SessionDto> JoinAsync(string code, string? displayName = null)
    {
        var response = await _client.PostAsJsonAsync("/api/rooms/join", new { code, displayName });
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<SessionDto>(Json))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"{(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }

    private static void Authorize(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record SessionDto(RoomDto Room, DeviceDto Device, string AccessToken);
    private sealed record RoomDto(Guid Id, string PublicCode);
    private sealed record DeviceDto(Guid Id, string DisplayName);
    private sealed record RoomDetailDto(RoomDto Room, List<DeviceDto> Devices);
    private sealed record TransferDto(Guid Id, Guid? TargetDeviceId, string? TextBody, List<DeliveryDto> Deliveries);
    private sealed record DeliveryDto(Guid DeviceId, DeliveryStatus Status);
    private sealed record DownloadDto(string Url);
}

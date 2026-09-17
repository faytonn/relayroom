using RelayRoom.Core.Domain;
using RelayRoom.Core.Errors;
using RelayRoom.Core.Rules;

namespace RelayRoom.UnitTests;

public class RoomCodeGeneratorTests
{
    [Fact]
    public void Create_UsesCrockfordAlphabetAndLength()
    {
        var code = RoomCodeGenerator.Create();
        Assert.Equal(RoomLimits.PublicCodeLength, code.Length);
        Assert.True(RoomCodeGenerator.IsValid(code));
    }

    [Theory]
    [InlineData("ab-c1o", "ABC10")]
    [InlineData("ilou", "110V")]
    public void Normalize_MapsAmbiguousCharacters(string input, string expected)
    {
        Assert.Equal(expected, RoomCodeGenerator.Normalize(input));
    }
}

public class RoomRulesTests
{
    [Fact]
    public void EnsureJoinable_RejectsExpiredRooms()
    {
        var room = new Room
        {
            Status = RoomStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        Assert.Throws<RoomExpiredException>(() => RoomRules.EnsureJoinable(room, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void EnsureHasCapacity_RejectsSixthDevice()
    {
        var room = new Room();
        for (var i = 0; i < RoomLimits.MaxDevices; i++)
        {
            room.Devices.Add(new Device { Id = Guid.NewGuid() });
        }

        Assert.Throws<RoomFullException>(() => RoomRules.EnsureHasCapacity(room));
    }

    [Fact]
    public void EnsureCanAcceptBytes_EnforcesFileAndRoomCaps()
    {
        var room = new Room { UsedBytes = 0, MaxBytes = RoomLimits.MaxRoomBytes };
        Assert.Throws<QuotaExceededException>(() => RoomRules.EnsureCanAcceptBytes(room, RoomLimits.MaxFileBytes + 1));

        room.UsedBytes = RoomLimits.MaxRoomBytes - 10;
        Assert.Throws<QuotaExceededException>(() => RoomRules.EnsureCanAcceptBytes(room, 11));
    }

    [Fact]
    public void ShouldWarnExpiring_OnlyOnceInsideWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var room = new Room
        {
            Status = RoomStatus.Active,
            ExpiresAt = now.AddMinutes(3),
            ExpiringWarningSent = false
        };

        Assert.True(RoomRules.ShouldWarnExpiring(room, now));
        room.ExpiringWarningSent = true;
        Assert.False(RoomRules.ShouldWarnExpiring(room, now));
    }
}

public class TransferRulesTests
{
    [Fact]
    public void ValidateHttpUrl_RequiresAbsoluteHttp()
    {
        Assert.Equal("https://example.com/a", TransferRules.ValidateHttpUrl("https://example.com/a"));
        Assert.Throws<ValidationException>(() => TransferRules.ValidateHttpUrl("ftp://example.com"));
        Assert.Throws<ValidationException>(() => TransferRules.ValidateHttpUrl("not-a-url"));
    }

    [Fact]
    public void EnsureSenderOrHostCanDelete_AllowsHostAndSenderOnly()
    {
        var transfer = new Transfer { SenderDeviceId = Guid.NewGuid() };
        var stranger = new Device { Id = Guid.NewGuid(), Role = DeviceRole.Guest };
        Assert.Throws<ForbiddenException>(() => TransferRules.EnsureSenderOrHostCanDelete(transfer, stranger));

        var host = new Device { Id = Guid.NewGuid(), Role = DeviceRole.Host };
        TransferRules.EnsureSenderOrHostCanDelete(transfer, host);
    }

    [Fact]
    public void MarkAvailable_CompletesBlob()
    {
        var transfer = new Transfer
        {
            Status = TransferStatus.Uploading,
            Blob = new BlobObject { UploadStatus = BlobUploadStatus.Pending }
        };

        TransferRules.MarkAvailable(transfer, DateTimeOffset.UtcNow);
        Assert.Equal(TransferStatus.Available, transfer.Status);
        Assert.Equal(BlobUploadStatus.Complete, transfer.Blob.UploadStatus);
    }
}

public class MimeAllowListTests
{
    [Fact]
    public void RejectsUnknownBinaryTypes()
    {
        Assert.Throws<ValidationException>(() => MimeAllowList.Normalize("application/x-msdownload", TransferKind.File));
        Assert.Equal("image/png", MimeAllowList.Normalize("image/png", TransferKind.Image));
        Assert.Throws<ValidationException>(() => MimeAllowList.Normalize("video/mp4", TransferKind.Voice));
    }
}

public class DeviceNameTests
{
    [Fact]
    public void FromUserAgent_DetectsMobileSafari()
    {
        var (kind, name) = DeviceName.FromUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1");
        Assert.Equal(DeviceKind.Mobile, kind);
        Assert.Contains("iPhone", name, StringComparison.OrdinalIgnoreCase);
    }
}

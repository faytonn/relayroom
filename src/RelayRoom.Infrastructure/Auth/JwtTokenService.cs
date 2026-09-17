using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RelayRoom.Core.Abstractions;
using RelayRoom.Core.Auth;
using RelayRoom.Core.Domain;

namespace RelayRoom.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "RelayRoom";
    public string Audience { get; set; } = "RelayRoom";
    public string SigningKey { get; set; } = string.Empty;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public DeviceToken Issue(Device device, Room room)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = room.ExpiresAt;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, device.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
            new Claim(DeviceClaims.RoomId, device.RoomId.ToString()),
            new Claim(DeviceClaims.Role, device.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString())
        };

        var jwt = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new DeviceToken(new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}

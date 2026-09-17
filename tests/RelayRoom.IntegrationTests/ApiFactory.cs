using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RelayRoom.Api.Hosting;
using Testcontainers.PostgreSql;

namespace RelayRoom.IntegrationTests;

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:relayroom", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:Issuer", "RelayRoom");
        builder.UseSetting("Jwt:Audience", "RelayRoom");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-32bytes!");
        builder.UseSetting("Tus:Path", Path.Combine(Path.GetTempPath(), "relayroom-tus-tests", Guid.NewGuid().ToString("N")));
        builder.UseSetting("RelayRoom:JoinPermitLimit", JoinPermitLimit.ToString());
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(s => s.ImplementationType == typeof(RoomExpiryHostedService)).ToList())
            {
                services.Remove(descriptor);
            }
        });
    }

    protected virtual int JoinPermitLimit => 1000;
}

public sealed class RateLimitedApiFactory : ApiFactory
{
    protected override int JoinPermitLimit => 2;
}

using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using RelayRoom.Core.Abstractions;
using RelayRoom.Infrastructure.Auth;
using RelayRoom.Infrastructure.Storage;
using RelayRoom.Infrastructure.Time;

namespace RelayRoom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        return services;
    }

    public static IServiceCollection AddAzureBlobs(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(_ => CreateBlobServiceClient(connectionString));
        services.AddSingleton<IBlobStorage, AzureBlobStorage>();
        return services;
    }

    public static IServiceCollection AddInMemoryBlobs(this IServiceCollection services)
    {
        services.AddSingleton<IBlobStorage, InMemoryBlobStorage>();
        return services;
    }

    internal static BlobServiceClient CreateBlobServiceClient(string connectionString)
    {
        if (LooksLikeAccountConnectionString(connectionString))
        {
            return new BlobServiceClient(connectionString);
        }

        // Aspire on Azure injects a blob endpoint URI plus managed identity.
        return new BlobServiceClient(new Uri(connectionString), new DefaultAzureCredential());
    }

    private static bool LooksLikeAccountConnectionString(string value) =>
        value.Contains("AccountKey=", StringComparison.OrdinalIgnoreCase)
        || value.Contains("SharedAccessSignature=", StringComparison.OrdinalIgnoreCase)
        || value.Contains("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase)
        || value.Contains("DefaultEndpointsProtocol=", StringComparison.OrdinalIgnoreCase);
}

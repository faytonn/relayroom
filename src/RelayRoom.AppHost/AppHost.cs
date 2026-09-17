var builder = DistributedApplication.CreateBuilder(args);

builder.AddAzureContainerAppEnvironment("aca");

// Local: Docker Postgres + pgAdmin. Publish: Azure Database for PostgreSQL (burstable).
var postgres = builder.AddAzurePostgresFlexibleServer("postgres")
    .WithPasswordAuthentication()
    .RunAsContainer(container => container.WithPgAdmin());

postgres.ConfigureInfrastructure(infra =>
{
    var server = infra.GetProvisionableResources()
        .OfType<Azure.Provisioning.PostgreSql.PostgreSqlFlexibleServer>()
        .Single();
    server.Sku = new Azure.Provisioning.PostgreSql.PostgreSqlFlexibleServerSku
    {
        Name = "Standard_B1ms",
        Tier = Azure.Provisioning.PostgreSql.PostgreSqlFlexibleServerSkuTier.Burstable
    };
    server.StorageSizeInGB = 32;
});

var db = postgres.AddDatabase("relayroom");

// Local: Azurite. Publish: a real storage account + container.
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

var api = builder.AddProject<Projects.RelayRoom_Api>("api")
    .WithReference(db)
    .WithReference(blobs)
    .WaitFor(db)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.RelayRoom_Worker>("worker")
    .WithReference(db)
    .WithReference(blobs)
    .WaitFor(db);

if (builder.ExecutionContext.IsPublishMode)
{
    var jwtKey = builder.AddParameter("jwt-signing-key", secret: true);
    api.WithEnvironment("Jwt__SigningKey", jwtKey);
}
else
{
    builder.AddViteApp("web", "../RelayRoom.Web")
        .WithPnpm()
        .WithReference(api)
        .WithExternalHttpEndpoints();
}

builder.Build().Run();

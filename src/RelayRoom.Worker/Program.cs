using RelayRoom.Application;
using RelayRoom.Application.Services;
using RelayRoom.Core.Abstractions;
using RelayRoom.Infrastructure;
using RelayRoom.Infrastructure.Notifications;
using RelayRoom.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("relayroom")
    ?? throw new InvalidOperationException("Connection string 'relayroom' is required.");

builder.Services.AddPersistence(connectionString);
builder.Services.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddSingleton<IRoomNotifier, NullRoomNotifier>();

var blobConnection = builder.Configuration.GetConnectionString("blobs");
if (!string.IsNullOrWhiteSpace(blobConnection))
{
    builder.Services.AddAzureBlobs(blobConnection);
}
else
{
    builder.Services.AddInMemoryBlobs();
}

builder.Services.AddHostedService<RelayRoom.Worker.Worker>();

var host = builder.Build();

if (builder.Environment.IsDevelopment())
{
    await using var scope = host.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

host.Run();

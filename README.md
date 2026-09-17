# RelayRoom

Temporary rooms for sending files, links, text, photos, and voice notes between browsers. The server is the relay. There is no app to install.

This repository includes an ASP.NET Core backend and a React UI in the quiet-instrument visual language.

## Stack

ASP.NET Core 10, SignalR, EF Core, PostgreSQL, tus, Azure Blob / Azurite, .NET Aspire, React + Vite, xUnit + Testcontainers.

## Run locally

1. Start Docker Desktop (Postgres + Azurite).
2. From the repo root: `dotnet run --project src/RelayRoom.AppHost` (API + worker + infra). Aspire looks for `pnpm` on PATH. On Windows, `corepack enable` often fails with EPERM because it tries to write shims into `C:\Program Files\nodejs`. Use a user-local shim instead: `corepack enable pnpm --install-directory "$env:LOCALAPPDATA\pnpm-shims"`, then add that folder to your user PATH and open a **new** terminal before starting AppHost.
3. In another terminal: `corepack pnpm --dir src/RelayRoom.Web install` then `corepack pnpm --dir src/RelayRoom.Web dev` (UI on http://localhost:5173, proxies `/api` and `/hubs` to the API on :5090). If Aspire is running and `pnpm` is on PATH, the AppHost starts Vite for you.
4. Scalar/OpenAPI is at `/scalar` on the API in Development.

## Useful requests

```http
POST /api/rooms
POST /api/rooms/join
GET  /api/rooms/{id}
POST /api/rooms/{id}/transfers
POST /api/uploads          (tus)
GET  /hubs/room            (SignalR, access_token query)
```

## Tests

```bash
dotnet test
```

Integration tests start PostgreSQL in Docker via Testcontainers. Docker Desktop must be running.

## Deploy to Azure (GitHub Actions)

The live site is Azure App Service `relayroom-fayton` (Poland Central). A push to `main` builds the React app, copies it into API `wwwroot`, runs tests, publishes the API, and zip-deploys. Connection strings and JWT stay in Azure App Settings — they are not in git.

One-time setup:

1. Create a GitHub repo and push `main` (this repo includes `.github/workflows/deploy-azure.yml`).
2. Azure Portal → **relayroom-fayton** → **Get publish profile**. Open the `.PublishSettings` file, copy the **entire XML**.
3. GitHub → repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:
   - Name: `AZURE_WEBAPP_PUBLISH_PROFILE`
   - Value: paste the XML
4. Keep App Service **startup command** `dotnet RelayRoom.Api.dll` and **Skip Server-Side Build** (`SCM_DO_BUILD_DURING_DEPLOYMENT=false`). GitHub builds; Azure only runs the zip.
5. Push to `main` (or **Actions** → **Deploy Azure App Service** → **Run workflow**).

Do not use App Service **Deployment Center** to auto-generate a workflow. Oryx cannot build this repo (ASP.NET + Vite in one site). The checked-in workflow is the pipeline.

The public URL is `https://www.relay-room.com` once DNS and the managed certificate are bound. Join/QR links use App Setting `RelayRoom__PublicBaseUrl`.

This is **not free forever**. Postgres is the expensive piece.

## Database migrations

API and Worker call `Database.MigrateAsync()` on startup in Development (Aspire and local `dotnet run`). Outside Development:

```bash
dotnet ef database update --project src/RelayRoom.Persistence --startup-project src/RelayRoom.Api
```

The initial schema is in `src/RelayRoom.Persistence/Migrations`.

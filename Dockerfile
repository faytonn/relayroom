# Single image: React SPA is built, then served from the API's wwwroot.

# --- Stage 1: build the SPA ---
FROM node:22-alpine AS web
WORKDIR /web
RUN corepack enable
COPY src/RelayRoom.Web/package.json src/RelayRoom.Web/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY src/RelayRoom.Web/ ./
RUN pnpm build

# --- Stage 2: publish the API ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /repo
COPY src/ src/
RUN dotnet restore src/RelayRoom.Api/RelayRoom.Api.csproj
COPY --from=web /web/dist/ src/RelayRoom.Api/wwwroot/
RUN dotnet publish src/RelayRoom.Api/RelayRoom.Api.csproj -c Release -o /out --no-restore

# --- Stage 3: runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /out ./
# .NET images run as the non-root "app" user; tus writes partial uploads under App_Data.
RUN mkdir -p /app/App_Data/tus && chown -R app:app /app/App_Data
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RelayRoom.Api.dll"]

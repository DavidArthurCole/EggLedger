# syntax=docker/dockerfile:1
# Builds the unified EggLedger Blazor Server host (app + folded-in sync server).
# The UI runs server-side over a SignalR circuit, so decode/crypto/storage are native;
# there is no static WASM bundle. Single build: docker build -t eggledger:latest .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props .editorconfig EggLedger.Csharp.slnx ./
COPY nuget-local/ nuget-local/
COPY src/ src/
RUN dotnet publish src/EggLedger.Web.Server/EggLedger.Web.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Binds SYNC_PORT via ASPNETCORE_URLS (compose). Migrations + bot start on boot when
# DATABASE_URL is set; egg-api is proxied to auxbrain in-process (no nginx CORS dodge).
ENTRYPOINT ["dotnet", "EggLedger.Web.Server.dll"]

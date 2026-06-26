# syntax=docker/dockerfile:1
# Builds the unified EggLedger Blazor Server host (app + folded-in sync server).
# The UI runs server-side over a SignalR circuit, so decode/crypto/storage are native;
# there is no static WASM bundle.
#
# SyncKit.* packages come from GitHub Packages (private feed). Restore needs a token with
# read:packages. CI passes it as a build secret (GITHUB_TOKEN); locally:
#   docker build --secret id=github_token,env=GITHUB_PACKAGES_PAT -t eggledger:latest .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props .editorconfig EggLedger.slnx ./
COPY src/ src/
# Inject the GitHub Packages credential for restore only (never baked into a layer): rewrite
# the github source with the token, restore, then the config is discarded with the build stage.
RUN --mount=type=secret,id=github_token \
    dotnet nuget update source github \
      --username DavidArthurCole \
      --password "$(cat /run/secrets/github_token)" \
      --store-password-in-clear-text \
      --configfile nuget.config \
    && dotnet publish src/EggLedger.Web.Server/EggLedger.Web.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# Npgsql probes GSSAPI/Kerberos at connect; the slim image lacks the lib (logs a load error
# even with password auth). Install it to silence the probe. Done as root before USER drop.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /app ./
# Run as the image's predefined non-root user (UID 1654); the app binds a high port
# (SYNC_PORT >= 1024 via ASPNETCORE_URLS), so no privileged bind is needed.
USER app
# Binds SYNC_PORT via ASPNETCORE_URLS (compose). Migrations + bot start on boot when
# DATABASE_URL is set; egg-api is proxied to auxbrain in-process (no nginx CORS dodge).
ENTRYPOINT ["dotnet", "EggLedger.Web.Server.dll"]

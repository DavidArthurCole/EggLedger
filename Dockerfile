# syntax=docker/dockerfile:1.7-labs
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props .editorconfig EggLedger.slnx ./
COPY --parents src/*/*.csproj ./
COPY src/EggLedger.Web/Styles/ src/EggLedger.Web/Styles/
COPY src/EggLedger.Web/tailwind.config.js src/EggLedger.Web/tailwind.config.js
RUN --mount=type=secret,id=github_token \
    --mount=type=cache,target=/root/.nuget/packages \
    dotnet nuget update source github \
      --username DavidArthurCole \
      --password "$(cat /run/secrets/github_token)" \
      --store-password-in-clear-text \
      --configfile nuget.config \
    && dotnet restore src/EggLedger.Web.Server/EggLedger.Web.Server.csproj
COPY src/ src/
ARG EGGLEDGER_VERSION
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/EggLedger.Web.Server/EggLedger.Web.Server.csproj -c Release -o /app \
      ${EGGLEDGER_VERSION:+-p:EggLedgerVersion=$EGGLEDGER_VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG GIT_SHA
ENV GIT_SHA=$GIT_SHA
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /app ./
USER app
ENTRYPOINT ["dotnet", "EggLedger.Web.Server.dll"]

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG EGGLEDGER_VERSION
COPY global.json nuget.config Directory.Build.props .editorconfig EggLedger.slnx ./
COPY src/ src/
RUN --mount=type=secret,id=github_token \
    dotnet nuget update source github \
      --username DavidArthurCole \
      --password "$(cat /run/secrets/github_token)" \
      --store-password-in-clear-text \
      --configfile nuget.config \
    && dotnet publish src/EggLedger.Web.Server/EggLedger.Web.Server.csproj -c Release -o /app \
      ${EGGLEDGER_VERSION:+-p:EggLedgerVersion=$EGGLEDGER_VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=app:app /app ./
USER app
ENTRYPOINT ["dotnet", "EggLedger.Web.Server.dll"]

# syntax=docker/dockerfile:1
# Builds the EggLedger Blazor WASM client and serves it as a static site via nginx.
# Single self-contained build: docker build -t eggledger:latest .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props .editorconfig EggLedger.Csharp.slnx ./
COPY src/ src/
RUN dotnet publish src/EggLedger.Web.Wasm/EggLedger.Web.Wasm.csproj -c Release -o /app

FROM nginx:1.27-alpine AS runtime
COPY --from=build /app/wwwroot /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80

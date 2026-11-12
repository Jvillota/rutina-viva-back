# Multi-stage build for the API and the Worker (same solution, one image per target).
# Usage: docker build --target api|worker -t rutaviva-<target> backend/
# Only public images and nuget.org are used.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props .editorconfig RutaViva.slnx ./
COPY src/RutaViva.Domain/RutaViva.Domain.csproj src/RutaViva.Domain/
COPY src/RutaViva.Application/RutaViva.Application.csproj src/RutaViva.Application/
COPY src/RutaViva.Infrastructure/RutaViva.Infrastructure.csproj src/RutaViva.Infrastructure/
COPY src/RutaViva.Api/RutaViva.Api.csproj src/RutaViva.Api/
COPY src/RutaViva.Worker/RutaViva.Worker.csproj src/RutaViva.Worker/
RUN dotnet restore src/RutaViva.Api/RutaViva.Api.csproj && dotnet restore src/RutaViva.Worker/RutaViva.Worker.csproj
COPY src/ src/
RUN dotnet publish src/RutaViva.Api/RutaViva.Api.csproj -c Release -o /out/api --no-restore \
 && dotnet publish src/RutaViva.Worker/RutaViva.Worker.csproj -c Release -o /out/worker --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
# curl is only used by the container healthcheck.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out/api .
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=5s --timeout=3s --start-period=20s --retries=12 CMD curl -fs http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "RutaViva.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS worker
WORKDIR /app
COPY --from=build /out/worker .
USER $APP_UID
ENTRYPOINT ["dotnet", "RutaViva.Worker.dll"]

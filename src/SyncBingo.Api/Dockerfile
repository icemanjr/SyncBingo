# Build context: repository root (shared build props live there).
#
# Multi-architecture (ADR 0011): linux/amd64 and linux/arm64. The build stage always runs on the build
# machine's own architecture (no emulation): a framework-dependent publish is portable .NET code, the same
# for every CPU. Only the runtime stage, which just copies files, takes the target architecture's base image.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project or package version changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/SyncBingo.Domain/SyncBingo.Domain.csproj src/SyncBingo.Domain/
COPY src/SyncBingo.Application/SyncBingo.Application.csproj src/SyncBingo.Application/
COPY src/SyncBingo.Infrastructure/SyncBingo.Infrastructure.csproj src/SyncBingo.Infrastructure/
COPY src/SyncBingo.Api/SyncBingo.Api.csproj src/SyncBingo.Api/
RUN dotnet restore src/SyncBingo.Api/SyncBingo.Api.csproj

COPY .editorconfig ./
COPY src/ src/
# No apphost: the image starts with `dotnet SyncBingo.Api.dll`, and a native launcher would be the build machine's.
RUN dotnet publish src/SyncBingo.Api/SyncBingo.Api.csproj --no-restore --configuration Release --output /app -p:UseAppHost=false

# Chiseled image: no shell or package manager, runs as a non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime
WORKDIR /app
COPY --from=build /app ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "SyncBingo.Api.dll"]

# Build context is the repository root (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, on project files only, so a source-only change reuses the package layer.
COPY backend/Directory.Build.props backend/
COPY backend/SupportCrm.sln backend/
COPY backend/src/SupportCrm.Domain/SupportCrm.Domain.csproj                 backend/src/SupportCrm.Domain/
COPY backend/src/SupportCrm.Application/SupportCrm.Application.csproj       backend/src/SupportCrm.Application/
COPY backend/src/SupportCrm.Infrastructure/SupportCrm.Infrastructure.csproj backend/src/SupportCrm.Infrastructure/
COPY backend/src/SupportCrm.Api/SupportCrm.Api.csproj                       backend/src/SupportCrm.Api/
COPY backend/tests/SupportCrm.Tests/SupportCrm.Tests.csproj                 backend/tests/SupportCrm.Tests/
RUN dotnet restore backend/SupportCrm.sln

COPY backend/ backend/
RUN dotnet publish backend/src/SupportCrm.Api -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The image runs as the non-root user the base image already provides.
USER $APP_UID
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SupportCrm.Api.dll"]

# ── Build stage ──
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY DotnetApi.sln .
COPY DotnetApi/DotnetApi.csproj DotnetApi/
RUN dotnet restore
COPY . .
RUN dotnet publish DotnetApi/DotnetApi.csproj -c Release -o /app/publish

# ── Development stage (used by docker-compose.override) ──
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /app
COPY . .
RUN dotnet restore
EXPOSE 8080
# Entrypoint overridden by docker-compose.override.yml

# ── Production stage ──
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "DotnetApi.dll"]

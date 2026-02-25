# ── Build stage ──
FROM mcr.microsoft.com/dotnet/sdk:9.0.101 AS build
WORKDIR /src
COPY DotnetApi.sln .
COPY DotnetApi/DotnetApi.csproj DotnetApi/
RUN dotnet restore
COPY . .
RUN dotnet publish DotnetApi/DotnetApi.csproj -c Release -o /app/publish

# ── Production stage ──
FROM mcr.microsoft.com/dotnet/aspnet:9.0.101 AS final

# Add non-root user for security
RUN groupadd -r dotnetuser && useradd -r -g dotnetuser dotnetuser

WORKDIR /app
COPY --from=build /app/publish .

# Set permissions
RUN chown -R dotnetuser:dotnetuser /app

# Run as non-root user
USER dotnetuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "DotnetApi.dll"]

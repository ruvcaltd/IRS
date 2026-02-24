# Use the official .NET SDK image to build and publish the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and restore as distinct layers
COPY ./src/IRS.Api/IRS.Api.csproj ./src/IRS.Api/
COPY ./src/IRS.Application/IRS.Application.csproj ./src/IRS.Application/
COPY ./src/IRS.Domain/IRS.Domain.csproj ./src/IRS.Domain/
COPY ./src/IRS.Infrastructure/IRS.Infrastructure.csproj ./src/IRS.Infrastructure/
RUN dotnet restore ./src/IRS.Api/IRS.Api.csproj

# Copy everything else and build
COPY ./src/ ./src/
WORKDIR /app/src/IRS.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Development stage (used by docker-compose.override)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development
WORKDIR /app
COPY . .
# development build; dependencies will be restored by dotnet watch/run in override
EXPOSE 8080
# Entrypoint overridden by docker-compose.override.yml

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IRS.Api.dll"]

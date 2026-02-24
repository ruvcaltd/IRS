# Use the official .NET SDK image to build and publish the app
FROM mcr.microsoft.com/dotnet/sdk:9.0.0 AS build
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

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0.0 AS final

# Add non-root user for security
RUN groupadd -r dotnetuser && useradd -r -g dotnetuser dotnetuser

WORKDIR /app
COPY --from=build /app/publish .

# Set permissions
RUN chown -R dotnetuser:dotnetuser /app

# Run as non-root user
USER dotnetuser

EXPOSE 8080
ENTRYPOINT ["dotnet", "IRS.Api.dll"]

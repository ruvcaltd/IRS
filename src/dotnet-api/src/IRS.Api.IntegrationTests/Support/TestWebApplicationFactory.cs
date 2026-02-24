using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using IRS.Infrastructure.Data;
using IRS.Application.Services;

namespace IRS.Api.IntegrationTests.Support;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddJsonFile("appsettings.Test.json");
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            services.RemoveAll<DbContextOptions<IrsDbContext>>();
            services.RemoveAll<IrsDbContext>();

            // Add test database connection
            services.AddDbContext<IrsDbContext>(options =>
            {
                options.UseSqlServer(_connectionString);
            });

            // Override any other services needed for testing
            // e.g., email service, external API clients, etc.
        });

        builder.UseEnvironment("Test");
    }
}

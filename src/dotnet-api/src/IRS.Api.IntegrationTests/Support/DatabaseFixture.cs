using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using IRS.Infrastructure.Data;
using Respawn;
using System.Threading.Tasks;

namespace IRS.Api.IntegrationTests.Support;

public class DatabaseFixture
{
    private static Respawner _respawner = null!;
    private static readonly object _lock = new();
    private static bool _initialized;
    
    public string ConnectionString { get; }

    public DatabaseFixture(IConfiguration configuration)
    {
        ConnectionString = configuration.GetConnectionString("TestConnection") 
            ?? throw new InvalidOperationException("TestConnection string not found");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            using var connection = new SqlConnection(ConnectionString);
            connection.Open();

            _respawner = Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer,
                SchemasToInclude = new[] { "dbo" },
                WithReseed = true
            }).GetAwaiter().GetResult();

            _initialized = true;
        }

        // Seed reference data (roles, etc.)
        await SeedReferenceDataAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
        
        // Reseed reference data after reset - ALWAYS reseed, don't check for existence
        await SeedReferenceDataAsync();
    }

    private async Task SeedReferenceDataAsync()
    {
        using var connection = new SqlConnection(ConnectionString);
        connection.Open();

        try
        {
            // First, try to delete existing TeamRoles to start fresh
            const string deleteTeamRolesQuery = "DELETE FROM dbo.TeamRoles;";
            var deleteCmd = new SqlCommand(deleteTeamRolesQuery, connection);
            try
            {
                deleteCmd.ExecuteNonQuery();
            }
            catch { /* Might fail if there are foreign key constraints, that's okay */ }

            // Reset identity seed to 0 so next insert starts at 1
            const string reseedQuery = "DBCC CHECKIDENT ('dbo.TeamRoles', RESEED, 0);";
            var reseedCmd = new SqlCommand(reseedQuery, connection);
            try
            {
                reseedCmd.ExecuteNonQuery();
            }
            catch { /* Might not work in all configurations */ }

            // Now insert the team roles - must include id 1, 2, 3 and also have a Pending role
            // Use SET IDENTITY_INSERT to ensure specific IDs
            const string insertTeamRolesQuery = @"
                SET IDENTITY_INSERT dbo.TeamRoles ON;
                INSERT INTO dbo.TeamRoles (id, name) 
                VALUES 
                    (1, 'Admin'),
                    (2, 'Contributor'),
                    (3, 'ReadOnly'),
                    (99, 'Pending');
                SET IDENTITY_INSERT dbo.TeamRoles OFF;
            ";
            var insertCmd = new SqlCommand(insertTeamRolesQuery, connection);
            insertCmd.ExecuteNonQuery();
            
            System.Diagnostics.Debug.WriteLine("Successfully seeded TeamRoles");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to seed TeamRoles: {ex.Message}");
            // Throw so tests fail obviously if seeding doesn't work
            throw;
        }

        try
        {
            // Seed system Roles if they don't exist  
            const string checkRolesQuery = "SELECT COUNT(*) FROM dbo.Roles";
            var rolesCmd = new SqlCommand(checkRolesQuery, connection);
            var rolesCount = (int?)rolesCmd.ExecuteScalar() ?? 0;

            if (rolesCount == 0)
            {
                const string insertRolesQuery = @"
                    INSERT INTO dbo.Roles (name) 
                    VALUES 
                        ('Admin'),
                        ('Team Admin'),
                        ('Analyst'),
                        ('User');
                ";
                var insertRolesCmd = new SqlCommand(insertRolesQuery, connection);
                insertRolesCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Warning: Failed to seed Roles: {ex.Message}");
            // Don't throw for Roles as they might not be critical
        }
    }
}


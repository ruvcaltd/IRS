using IRS.Infrastructure;
using IRS.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace IRS.Api.IntegrationTests.Helpers;

public class TestDataBuilder
{
    private readonly IServiceProvider _serviceProvider;

    public TestDataBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<User> CreateUserAsync(
        string email = "test@example.com",
        string fullName = "Test User")
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IrsDbContext>();

        var user = new User
        {
            email = email,
            full_name = fullName,
            password_hash = BCrypt.Net.BCrypt.HashPassword("Pass123!"),
            created_at = DateTime.UtcNow,
            is_deleted = false
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
    }

    public async Task<Team> CreateTeamAsync(
        string name = "Test Team")
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IrsDbContext>();

        var team = new Team
        {
            name = name,
            created_at = DateTime.UtcNow,
            is_deleted = false
        };

        dbContext.Teams.Add(team);
        await dbContext.SaveChangesAsync();

        return team;
    }

    public async Task AddTeamMemberAsync(int userId, int teamId, int teamRoleId)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IrsDbContext>();

        var teamMember = new TeamMember
        {
            user_id = userId,
            team_id = teamId,
            team_role_id = teamRoleId,
            status = "active",
            created_at = DateTime.UtcNow,
            is_deleted = false
        };

        dbContext.TeamMembers.Add(teamMember);
        await dbContext.SaveChangesAsync();
    }
}

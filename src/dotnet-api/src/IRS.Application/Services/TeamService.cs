using IRS.Application.DTOs.Teams;
using IRS.Infrastructure.Data;
using IRS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IRS.Application.Services;

public class TeamService : ITeamService
{
    private readonly IrsDbContext _context;
    private readonly ILogger<TeamService> _logger;

    public TeamService(IrsDbContext context, ILogger<TeamService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TeamResponse> CreateTeamAsync(int userId, CreateTeamRequest request)
    {
        try
        {
            // Verify team name doesn't already exist
            var existingTeam = await _context.Teams
                .FirstOrDefaultAsync(t => t.name == request.name && !t.is_deleted);
            
            if (existingTeam != null)
            {
                throw new ArgumentException("Team with this name already exists");
            }

            // Create the team
            var team = new Team
            {
                name = request.name,
                created_at = DateTime.UtcNow,
                is_deleted = false
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            // Add creator as Admin member
            var teamMember = new TeamMember
            {
                user_id = userId,
                team_id = team.id,
                team_role_id = 1, // Admin role
                status = "ACTIVE",
                created_at = DateTime.UtcNow,
                approved_by_user_id = userId,
                approved_at = DateTime.UtcNow
            };

            _context.TeamMembers.Add(teamMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Team '{TeamName}' created by user {UserId}", team.name, userId);

            return new TeamResponse
            {
                id = team.id,
                name = team.name,
                created_at = team.created_at ?? DateTime.UtcNow,
                current_user_role = "Admin"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<TeamMemberResponse> JoinTeamAsync(int userId, JoinTeamRequest request)
    {
        try
        {
            // Verify team exists
            var team = await _context.Teams.FirstOrDefaultAsync(t => t.id == request.team_id && !t.is_deleted);
            if (team == null)
            {
                throw new ArgumentException("Team not found");
            }

            // Check if user is already a member
            var existingMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == request.team_id);
            
            if (existingMember != null)
            {
                throw new ArgumentException("User is already a member of this team");
            }

            // Create pending membership request
            // Use the "Pending" role (ID 99) temporarily until admin approves with an actual role
            var teamMember = new TeamMember
            {
                user_id = userId,
                team_id = request.team_id,
                team_role_id = 99, // Pending role placeholder
                status = "PENDING",
                created_at = DateTime.UtcNow
            };

            _context.TeamMembers.Add(teamMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {UserId} requested to join team {TeamId}", userId, request.team_id);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.id == userId);
            
            return new TeamMemberResponse
            {
                user_id = userId,
                team_id = request.team_id,
                team_role_id = 99,
                team_role_name = "Pending",
                user_full_name = user?.full_name ?? "Unknown",
                user_email = user?.email ?? "unknown@example.com",
                status = "PENDING",
                created_at = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining team: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<TeamMemberResponse>> GetPendingRequestsAsync(int teamId, int userId)
    {
        try
        {
            // Verify user is admin of this team
            var userRole = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
            
            if (userRole == null || userRole.team_role_id != 1) // 1 = Admin
            {
                throw new UnauthorizedAccessException("Only team admins can view pending requests");
            }

            // Get pending requests
            var pendingRequests = await _context.TeamMembers
                .Where(tm => tm.team_id == teamId && tm.status == "PENDING")
                .Include(tm => tm.user)
                .Select(tm => new TeamMemberResponse
                {
                    user_id = tm.user_id,
                    team_id = tm.team_id,
                    team_role_id = tm.team_role_id,
                    team_role_name = "Pending",
                    user_full_name = tm.user!.full_name,
                    user_email = tm.user.email,
                    status = tm.status,
                    created_at = tm.created_at ?? DateTime.UtcNow
                })
                .ToListAsync();

            return pendingRequests;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending requests: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<TeamMemberResponse> ApproveMemberAsync(int teamId, int userId, ApproveMemberRequest request)
    {
        try
        {
            // Verify user is admin of this team
            var userRole = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
            
            if (userRole == null || userRole.team_role_id != 1) // 1 = Admin
            {
                throw new UnauthorizedAccessException("Only team admins can approve members");
            }

            // Find the pending membership request
            var pendingMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.user_id == request.user_id && tm.team_id == teamId && tm.status == "PENDING");
            
            if (pendingMember == null)
            {
                throw new ArgumentException("Pending membership request not found");
            }

            // Approve and assign role
            pendingMember.status = "ACTIVE";
            pendingMember.team_role_id = request.team_role_id;
            pendingMember.approved_by_user_id = userId;
            pendingMember.approved_at = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("User {AdminId} approved user {UserId} to team {TeamId} with role {RoleId}", 
                userId, request.user_id, teamId, request.team_role_id);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.id == request.user_id);
            var roleName = GetTeamRoleName(request.team_role_id);

            return new TeamMemberResponse
            {
                user_id = request.user_id,
                team_id = teamId,
                team_role_id = request.team_role_id,
                team_role_name = roleName,
                user_full_name = user?.full_name ?? "Unknown",
                user_email = user?.email ?? "unknown@example.com",
                status = "ACTIVE",
                created_at = pendingMember.created_at ?? DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving member: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<bool> RejectMemberAsync(int teamId, int userId, int requestUserId)
    {
        try
        {
            // Verify user is admin of this team
            var userRole = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
            
            if (userRole == null || userRole.team_role_id != 1) // 1 = Admin
            {
                throw new UnauthorizedAccessException("Only team admins can reject members");
            }

            // Find and delete the pending membership request
            var pendingMember = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.user_id == requestUserId && tm.team_id == teamId && tm.status == "PENDING");
            
            if (pendingMember == null)
            {
                throw new ArgumentException("Pending membership request not found");
            }

            _context.TeamMembers.Remove(pendingMember);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {AdminId} rejected user {UserId} from team {TeamId}", 
                userId, requestUserId, teamId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting member: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<TeamMemberResponse>> GetTeamMembersAsync(int teamId, int userId)
    {
        try
        {
            // Verify user is member of this team
            var userMembership = await _context.TeamMembers
                .FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
            
            if (userMembership == null)
            {
                throw new UnauthorizedAccessException("User is not a member of this team");
            }

            // Get active members
            var members = await _context.TeamMembers
                .Where(tm => tm.team_id == teamId && tm.status == "ACTIVE")
                .Include(tm => tm.user)
                .ToListAsync();

            // Map to response - do this in memory
            return members.Select(tm => new TeamMemberResponse
            {
                user_id = tm.user_id,
                team_id = tm.team_id,
                team_role_id = tm.team_role_id,
                team_role_name = GetTeamRoleName(tm.team_role_id),
                user_full_name = tm.user!.full_name,
                user_email = tm.user.email,
                status = tm.status,
                created_at = tm.created_at ?? DateTime.UtcNow
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team members: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<TeamResponse>> GetUserTeamsAsync(int userId)
    {
        try
        {
            var memberships = await _context.TeamMembers
                .Where(tm => tm.user_id == userId && tm.status == "ACTIVE")
                .Include(tm => tm.team)
                .ToListAsync();

            var teams = memberships.Select(tm => new TeamResponse
            {
                id = tm.team!.id,
                name = tm.team.name,
                created_at = tm.team.created_at ?? DateTime.UtcNow,
                current_user_role = GetTeamRoleName(tm.team_role_id)
            }).ToList();

            return teams;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user teams: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<IEnumerable<TeamResponse>> SearchTeamsAsync(string query)
    {
        try
        {
            var teams = await _context.Teams
                .Where(t => !t.is_deleted && t.name.Contains(query))
                .Select(t => new TeamResponse
                {
                    id = t.id,
                    name = t.name,
                    created_at = t.created_at ?? DateTime.UtcNow,
                    current_user_role = ""
                })
                .Take(10)
                .ToListAsync();

            return teams;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching teams: {Message}", ex.Message);
            throw;
        }
    }

    private string GetTeamRoleName(int roleId) => roleId switch
    {
        1 => "Admin",
        2 => "Contributor",
        3 => "ReadOnly",
        _ => "Unknown"
    };
}

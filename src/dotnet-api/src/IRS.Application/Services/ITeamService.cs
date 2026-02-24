using IRS.Application.DTOs.Teams;

namespace IRS.Application.Services;

public interface ITeamService
{
    Task<TeamResponse> CreateTeamAsync(int userId, CreateTeamRequest request);
    Task<TeamMemberResponse> JoinTeamAsync(int userId, JoinTeamRequest request);
    Task<IEnumerable<TeamMemberResponse>> GetPendingRequestsAsync(int teamId, int userId);
    Task<TeamMemberResponse> ApproveMemberAsync(int teamId, int userId, ApproveMemberRequest request);
    Task<bool> RejectMemberAsync(int teamId, int userId, int requestUserId);
    Task<IEnumerable<TeamMemberResponse>> GetTeamMembersAsync(int teamId, int userId);
    Task<IEnumerable<TeamResponse>> GetUserTeamsAsync(int userId);
    Task<IEnumerable<TeamResponse>> SearchTeamsAsync(string query);
}

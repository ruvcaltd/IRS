using IRS.Application.DTOs.Teams;
using IRS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/v1/teams")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly ITeamService _teamService;
    private readonly ILogger<TeamsController> _logger;

    public TeamsController(
        ITeamService teamService,
        ILogger<TeamsController> logger)
    {
        _teamService = teamService;
        _logger = logger;
    }

    /// <summary>
    /// Get the current user's ID from JWT claims
    /// </summary>
    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
    }

    /// <summary>
    /// Create a new team
    /// </summary>
    /// <param name="request">Team creation details</param>
    /// <returns>Created team information</returns>
    /// <response code="201">Team successfully created</response>
    /// <response code="400">Invalid input</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TeamResponse>> CreateTeam([FromBody] CreateTeamRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("Unable to determine user identity");
            }

            var response = await _teamService.CreateTeamAsync(userId, request);
            return CreatedAtAction(nameof(GetUserTeams), response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Team creation validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An error occurred while creating the team" });
        }
    }

    /// <summary>
    /// Search for teams by name
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>List of matching teams</returns>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TeamResponse>>> SearchTeams([FromQuery] string query = "")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query is required");
            }

            var teams = await _teamService.SearchTeamsAsync(query);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching teams");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while searching teams" });
        }
    }

    /// <summary>
    /// Request to join a team
    /// </summary>
    /// <param name="request">Join request details</param>
    /// <returns>Team membership request status</returns>
    /// <response code="202">Request to join accepted and pending approval</response>
    /// <response code="400">Invalid input or user already a member</response>
    /// <response code="401">Unauthorized</response>
    [HttpPost("join")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TeamMemberResponse>> JoinTeam([FromBody] JoinTeamRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("Unable to determine user identity");
            }

            var response = await _teamService.JoinTeamAsync(userId, request);
            return Accepted(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Join team validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining team");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while processing the join request" });
        }
    }

    /// <summary>
    /// Get pending join requests for a team (Admin only)
    /// </summary>
    /// <param name="teamId">Team ID</param>
    /// <returns>List of pending membership requests</returns>
    /// <response code="200">List of pending requests</response>
    /// <response code="403">User is not an admin of the team</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet("{teamId}/requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TeamMemberResponse>>> GetPendingRequests(int teamId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("Unable to determine user identity");
            }

            var requests = await _teamService.GetPendingRequestsAsync(teamId, userId);
            return Ok(requests);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access to pending requests: {Message}", ex.Message);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending requests");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving pending requests" });
        }
    }

    /// <summary>
    /// Approve a user to join the team (Admin only)
    /// </summary>
    /// <param name="teamId">Team ID</param>
    /// <param name="requestUserId">User ID to approve</param>
    /// <param name="request">Approval details with role assignment</param>
    /// <returns>Updated team membership</returns>
    /// <response code="200">Member successfully approved</response>
    /// <response code="403">User is not an admin of the team</response>
    /// <response code="404">Pending request not found</response>
    /// <response code="401">Unauthorized</response>
    [HttpPut("{teamId}/requests/{requestUserId}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TeamMemberResponse>> ApproveMember(
        int teamId,
        int requestUserId,
        [FromBody] ApproveMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("Unable to determine user identity");
            }

            request.user_id = requestUserId;
            var response = await _teamService.ApproveMemberAsync(teamId, userId, request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized approval attempt: {Message}", ex.Message);
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Member approval validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving member");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while approving the member" });
        }
    }

    /// <summary>
    /// Reject a join request (Admin only)
    /// </summary>
    /// <param name="teamId">Team ID</param>
    /// <param name="requestUserId">User ID to reject</param>
    /// <returns>Success result</returns>
    /// <response code="204">Request successfully rejected</response>
    /// <response code="403">User is not an admin of the team</response>
    /// <response code="404">Pending request not found</response>
    /// <response code="401">Unauthorized</response>
    [HttpDelete("{teamId}/requests/{requestUserId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RejectMember(int teamId, int requestUserId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("Unable to determine user identity");
            }

            await _teamService.RejectMemberAsync(teamId, userId, requestUserId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized rejection attempt: {Message}", ex.Message);
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Member rejection validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting member");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while rejecting the member" });
        }
    }

    /// <summary>
    /// Get members of a team
    /// </summary>
    /// <param name="teamId">Team ID</param>
    /// <returns>List of active team members</returns>
    /// <response code="200">List of team members</response>
    /// <response code="403">User is not a member of the team</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet("{teamId}/members")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TeamMemberResponse>>> GetTeamMembers(int teamId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("Unable to determine user identity");
            }

            var members = await _teamService.GetTeamMembersAsync(teamId, userId);
            return Ok(members);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized access to team members: {Message}", ex.Message);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting team members");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving team members" });
        }
    }

    /// <summary>
    /// Get all teams for the current user
    /// </summary>
    /// <returns>List of user's teams</returns>
    /// <response code="200">List of user's teams</response>
    /// <response code="401">Unauthorized</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<TeamResponse>>> GetUserTeams()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0)
            {
                return Unauthorized("Unable to determine user identity");
            }

            var teams = await _teamService.GetUserTeamsAsync(userId);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user teams");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving user teams" });
        }
    }
}

using IRS.Application.DTOs.Agents;
using IRS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class AgentsController : ControllerBase
{
    private readonly IResearchAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(IResearchAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
    }

    /// <summary>
    /// List available agents for a team (team-visible and user's private agents).
    /// </summary>
    [HttpGet("teams/{teamId}/agents/available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<AvailableAgentResponse>>> GetAvailableAgents(int teamId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var agents = await _agentService.GetAvailableAgentsForTeamAsync(userId, teamId);
            return Ok(agents);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to list available agents");
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing available agents");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Validate an agent payload without creating it (dry run)
    /// </summary>
    [HttpPost("teams/{teamId}/agents/validate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AgentValidationResponse>> ValidateAgent(int teamId, [FromBody] CreateAgentRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var result = await _agentService.ValidateAgentAsync(userId, teamId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to validate agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Validate agent validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Enqueue a run for a page agent
    /// </summary>
    [HttpPost("page-agents/{pageAgentId}/runs")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentRunResponse>> EnqueueRun(int pageAgentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var response = await _agentService.EnqueueRunAsync(userId, pageAgentId);
            return CreatedAtAction(nameof(GetRuns), new { pageAgentId }, response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to enqueue run");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Enqueue run validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing run");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Create a new agent for a team (Private or Team visibility).
    /// </summary>
    [HttpPost("teams/{teamId}/agents")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AvailableAgentResponse>> CreateAgent(int teamId, [FromBody] CreateAgentRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var agent = await _agentService.CreateAgentAsync(userId, teamId, request);
            return CreatedAtAction(nameof(GetAvailableAgents), new { teamId }, agent);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to create agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Create agent validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get a single agent's full details (non-sensitive fields).
    /// </summary>
    [HttpGet("teams/{teamId}/agents/{agentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentDetailResponse>> GetAgent(int teamId, int agentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var agent = await _agentService.GetAgentAsync(userId, teamId, agentId);
            return Ok(agent);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to get agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Get agent validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Update an existing agent. Only the owner or a team admin may update.
    /// </summary>
    [HttpPut("teams/{teamId}/agents/{agentId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvailableAgentResponse>> UpdateAgent(int teamId, int agentId, [FromBody] CreateAgentRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var agent = await _agentService.UpdateAgentAsync(userId, teamId, agentId, request);
            return Ok(agent);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to update agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Update agent validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }
    /// <summary>
    /// Attach an agent to a research page.
    /// </summary>
    [HttpPost("research-pages/{pageId}/agents")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageAgentResponse>> AttachAgent(int pageId, [FromBody] AttachAgentRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var response = await _agentService.AttachAgentAsync(userId, pageId, request.agent_id);
            return CreatedAtAction(nameof(GetPageAgents), new { pageId }, response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to attach agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Attach agent validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// List agents attached to a research page.
    /// </summary>
    [HttpGet("research-pages/{pageId}/agents")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<PageAgentResponse>>> GetPageAgents(int pageId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var agents = await _agentService.GetPageAgentsAsync(userId, pageId);
            return Ok(agents);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to list page agents");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Get page agents validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing page agents");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Attach an agent to a section.
    /// </summary>
    [HttpPost("sections/{sectionId}/agents")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SectionAgentResponse>> AttachSectionAgent(int sectionId, [FromBody] AttachAgentRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var response = await _agentService.AttachSectionAgentAsync(userId, sectionId, request.agent_id);
            return CreatedAtAction(nameof(GetSectionAgents), new { sectionId }, response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to attach section agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Attach section agent validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error attaching section agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// List agents attached to a section.
    /// </summary>
    [HttpGet("sections/{sectionId}/agents")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<SectionAgentResponse>>> GetSectionAgents(int sectionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var agents = await _agentService.GetSectionAgentsAsync(userId, sectionId);
            return Ok(agents);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to list section agents");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Get section agents validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing section agents");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Enable or disable a page agent.
    /// </summary>
    [HttpPut("page-agents/{pageAgentId}/enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PageAgentResponse>> ToggleEnabled(int pageAgentId, [FromBody] ToggleAgentRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var response = await _agentService.SetAgentEnabledAsync(userId, pageAgentId, request.is_enabled);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to toggle agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Toggle agent validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Enable or disable a section agent.
    /// </summary>
    [HttpPut("section-agents/{sectionAgentId}/enabled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SectionAgentResponse>> ToggleSectionEnabled(int sectionAgentId, [FromBody] ToggleAgentRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var response = await _agentService.SetSectionAgentEnabledAsync(userId, sectionAgentId, request.is_enabled);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to toggle section agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Toggle section agent validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling section agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a page-level agent association and all its runs.
    /// </summary>
    [HttpDelete("page-agents/{pageAgentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeletePageAgent(int pageAgentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            await _agentService.DeletePageAgentAsync(userId, pageAgentId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to delete page agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Delete page agent validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting page agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a section-level agent association and all its runs.
    /// </summary>
    [HttpDelete("section-agents/{sectionAgentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteSectionAgent(int sectionAgentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            await _agentService.DeleteSectionAgentAsync(userId, sectionAgentId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to delete section agent");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Delete section agent validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting section agent");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// List runs for a page agent.
    /// </summary>
    [HttpGet("page-agents/{pageAgentId}/runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AgentRunResponse>>> GetRuns(int pageAgentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var runs = await _agentService.GetAgentRunsAsync(userId, pageAgentId);
            return Ok(runs);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to list agent runs");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Get agent runs validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing agent runs");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get the most recent run for a page agent.
    /// </summary>
    [HttpGet("page-agents/{pageAgentId}/runs/latest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentRunResponse?>> GetLatestRun(int pageAgentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var run = await _agentService.GetLatestAgentRunAsync(userId, pageAgentId);
            return Ok(run);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to list agent runs");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Get latest agent run validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing latest agent run");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Enqueue a run for a section agent
    /// </summary>
    [HttpPost("section-agents/{sectionAgentId}/runs")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentRunResponse>> EnqueueSectionRun(int sectionAgentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var response = await _agentService.EnqueueSectionRunAsync(userId, sectionAgentId);
            return CreatedAtAction(nameof(GetSectionRuns), new { sectionAgentId }, response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to enqueue section run");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Enqueue section run validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enqueuing section run");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// List runs for a section agent.
    /// </summary>
    [HttpGet("section-agents/{sectionAgentId}/runs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AgentRunResponse>>> GetSectionRuns(int sectionAgentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var runs = await _agentService.GetSectionAgentRunsAsync(userId, sectionAgentId);
            return Ok(runs);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to list section agent runs");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Get section agent runs validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing section agent runs");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }
}

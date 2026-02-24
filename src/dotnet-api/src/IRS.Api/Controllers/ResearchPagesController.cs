using IRS.Application.DTOs.Agents;
using IRS.Application.DTOs.Research;
using IRS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/v1/research-pages")]
[Authorize]
public class ResearchPagesController : ControllerBase
{
    private readonly IResearchService _researchService;
    private readonly IResearchAgentService _agentService;
    private readonly ILogger<ResearchPagesController> _logger;

    public ResearchPagesController(IResearchService researchService, IResearchAgentService agentService, ILogger<ResearchPagesController> logger)
    {
        _researchService = researchService;
        _agentService = agentService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
    }

    /// <summary>
    /// List research pages visible to the current user (teams where user is ACTIVE).
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<ResearchPageListItemResponse>>> GetMy()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var pages = await _researchService.GetMyResearchPagesAsync(userId);
            return Ok(pages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving my research pages");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create a research page for a team and security; auto-generates default sections.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResearchPageResponse>> Create([FromBody] CreateResearchPageRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var page = await _researchService.CreateResearchPageAsync(userId, request);
            return CreatedAtAction(nameof(GetById), new { id = page.id }, page);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to create research page");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Create research page validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating research page");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get a research page by id including sections.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResearchPageResponse>> GetById(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var page = await _researchService.GetResearchPageAsync(userId, id);
            return Ok(page);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to get research page");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Get research page validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting research page");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a research page and all associated agents, sections and runs.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            await _researchService.DeleteResearchPageAsync(userId, id);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to delete research page");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Delete research page validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting research page");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Enqueue runs for all page agents and all section agents attached to a research page.
    /// </summary>
    [HttpPost("{id}/run-all")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<AgentRunResponse>>> RunAll(int id)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var runs = await _agentService.RunAllAgentsForPageAsync(userId, id);
            return Ok(runs);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to run all agents");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Run all agents validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running all agents");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Recalculate fundamental and conviction scores for all active research pages accessible to the user.
    /// Averages latest agent run scores from section agents to update section scores,
    /// then averages section scores to update page-level scores.
    /// </summary>
    [HttpPost("recalculate-scores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> RecalculateScores()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            await _researchService.RecalculateAllScoresAsync(userId);
            return Ok(new { message = "Scores recalculated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating scores");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }
}

using IRS.Application.DTOs.Research;
using IRS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/v1/sections")]
[Authorize]
public class SectionsController : ControllerBase
{
    private readonly IResearchService _researchService;
    private readonly ILogger<SectionsController> _logger;

    public SectionsController(IResearchService researchService, ILogger<SectionsController> logger)
    {
        _researchService = researchService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
    }

    /// <summary>
    /// Add a comment to a section in a research page.
    /// </summary>
    [HttpPost("{sectionId}/comments")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentResponse>> AddComment(int sectionId, [FromBody] CreateCommentRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var response = await _researchService.AddCommentAsync(userId, sectionId, request);
            return CreatedAtAction(nameof(AddComment), new { sectionId, id = response.id }, response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to add comment");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Add comment validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// List comments for a section.
    /// </summary>
    [HttpGet("{sectionId}/comments")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CommentResponse>>> GetComments(int sectionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var comments = await _researchService.GetCommentsAsync(userId, sectionId);
            return Ok(comments);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to get comments");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Get comments validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting comments");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Create a new section in a research page.
    /// </summary>
    [HttpPost("research-page/{researchPageId}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SectionResponse>> CreateSection(int researchPageId, [FromBody] CreateSectionRequest request)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            var response = await _researchService.CreateSectionAsync(userId, researchPageId, request);
            return CreatedAtAction(nameof(CreateSection), new { researchPageId, id = response.id }, response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to create section");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Create section validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating section");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a section from a research page.
    /// </summary>
    [HttpDelete("{sectionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSection(int sectionId)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == 0) return Unauthorized(new { error = "Unable to determine user identity" });

            await _researchService.DeleteSectionAsync(userId, sectionId);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized to delete section");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Delete section validation failed: {Message}", ex.Message);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting section");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }
}

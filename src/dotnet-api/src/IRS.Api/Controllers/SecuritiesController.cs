using IRS.Application.DTOs.Securities;
using IRS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/v1/securities")]
[Authorize]
public class SecuritiesController : ControllerBase
{
    private readonly ISecurityService _securityService;
    private readonly ILogger<SecuritiesController> _logger;

    public SecuritiesController(ISecurityService securityService, ILogger<SecuritiesController> logger)
    {
        _securityService = securityService;
        _logger = logger;
    }

    /// <summary>
    /// Search securities by ticker or name. Uses local cache; may proxy OpenFIGI when configured.
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<SecuritySearchItem>>> Search([FromQuery] string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q)) return BadRequest(new { error = "Query is required" });
            var items = await _securityService.SearchAsync(q.Trim());
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching securities");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "An error occurred" });
        }
    }
}

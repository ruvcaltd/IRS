using Microsoft.AspNetCore.Mvc;
using IRS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IrsDbContext _context;

    public HealthController(IrsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            // Test database connection
            await _context.Database.CanConnectAsync();
            
            return Ok(new
            {
                status = "healthy",
                timestamp = DateTime.UtcNow,
                database = "connected"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "unhealthy",
                timestamp = DateTime.UtcNow,
                database = "disconnected",
                error = ex.Message
            });
        }
    }
}

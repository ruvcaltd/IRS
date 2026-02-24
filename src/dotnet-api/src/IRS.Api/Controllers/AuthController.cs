using IRS.Application.DTOs.Auth;
using IRS.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user with email, password, and full name.
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>User information and JWT token</returns>
    /// <response code="201">User successfully registered</response>
    /// <response code="400">Invalid input or email already exists</response>
    /// <response code="500">Server error</response>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.RegisterAsync(request);
            return CreatedAtAction(nameof(Register), response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Registration validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration logic error: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration");
            return StatusCode(500, new { error = "An unexpected error occurred." });
        }
    }

    /// <summary>
    /// Login with email and password to obtain JWT token.
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>User information and JWT token</returns>
    /// <response code="200">Login successful</response>
    /// <response code="400">Missing credentials</response>
    /// <response code="401">Invalid credentials</response>
    /// <response code="500">Server error</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Login validation failed: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed: {Message}", ex.Message);
            return Unauthorized(new { error = "Invalid email or password." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return StatusCode(500, new { error = "An unexpected error occurred." });
        }
    }
}

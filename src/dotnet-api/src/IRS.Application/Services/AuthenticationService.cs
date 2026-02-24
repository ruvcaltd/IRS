using IRS.Application.DTOs.Auth;
using IRS.Infrastructure.Data;
using IRS.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace IRS.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IrsDbContext _context;
    private readonly string _jwtKey;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;
    private readonly int _jwtExpiryMinutes;

    public AuthenticationService(
        IrsDbContext context,
        string jwtKey,
        string jwtIssuer,
        string jwtAudience,
        int jwtExpiryMinutes)
    {
        _context = context;
        _jwtKey = jwtKey;
        _jwtIssuer = jwtIssuer;
        _jwtAudience = jwtAudience;
        _jwtExpiryMinutes = jwtExpiryMinutes;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ArgumentException("Email, password, and full name are required.");
        }

        // Check if email already exists
        var existingUser = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.email == request.Email);

        if (existingUser != null)
        {
            throw new InvalidOperationException("Email already registered.");
        }

        // Hash password
        var passwordHash = HashPassword(request.Password);

        // Get default User role (system role)
        var userRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.name == "User");

        if (userRole == null)
        {
            throw new InvalidOperationException("Default user role not found.");
        }

        // Create user
        var user = new User
        {
            email = request.Email,
            password_hash = passwordHash,
            full_name = request.FullName,
            role_id = userRole.id,
            created_at = DateTime.UtcNow,
            is_deleted = false
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Generate JWT token
        var token = GenerateJwtToken(user.id, user.email);

        return new AuthResponse
        {
            UserId = user.id,
            Email = user.email,
            FullName = user.full_name,
            Token = token,
            ExpiresIn = _jwtExpiryMinutes * 60
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ArgumentException("Email and password are required.");
        }

        // Find user by email
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.email == request.Email && !u.is_deleted);

        if (user == null || !VerifyPassword(request.Password, user.password_hash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Generate JWT token
        var token = GenerateJwtToken(user.id, user.email);

        return new AuthResponse
        {
            UserId = user.id,
            Email = user.email ?? string.Empty,
            FullName = user.full_name ?? string.Empty,
            Token = token,
            ExpiresIn = _jwtExpiryMinutes * 60
        };
    }

    public async Task<bool> VerifyEmailExistsAsync(string email)
    {
        return await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.email == email && !u.is_deleted);
    }

    private string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    private string GenerateJwtToken(int userId, string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new System.Security.Claims.Claim("sub", userId.ToString()),
            new System.Security.Claims.Claim("email", email),
            new System.Security.Claims.Claim("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtIssuer,
            audience: _jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

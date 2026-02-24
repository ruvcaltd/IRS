using IRS.Application.DTOs.Auth;

namespace IRS.Application.Services;

public interface IAuthenticationService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<bool> VerifyEmailExistsAsync(string email);
}

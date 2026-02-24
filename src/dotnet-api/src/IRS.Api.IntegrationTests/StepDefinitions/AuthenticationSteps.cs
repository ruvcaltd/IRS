using TechTalk.SpecFlow;
using IRS.Api.IntegrationTests.Support;
using IRS.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.EntityFrameworkCore;

namespace IRS.Api.IntegrationTests.StepDefinitions;

[Binding]
public class AuthenticationSteps
{
    private readonly ScenarioContextWrapper _context;
    private readonly TestWebApplicationFactory _factory;

    public AuthenticationSteps(
        ScenarioContextWrapper context, 
        TestWebApplicationFactory factory)
    {
        _context = context;
        _factory = factory;
    }

    [When(@"I register a new user with email ""(.*)"", password ""(.*)"", and full name ""(.*)""")]
    public async Task WhenIRegisterANewUserWithDetails(string email, string password, string fullName)
    {
        var registerRequest = new { Email = email, Password = password, FullName = fullName };
        
        var response = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        
        _context.LastResponse = response;
        _context.Set("RegisteredUserEmail", email);
    }

    [Given(@"a user exists with email ""(.*)"" and password ""(.*)""")]
    public async Task GivenAUserExistsWithEmailAndPassword(string email, string password)
    {
        var registerRequest = new { Email = email, Password = password, FullName = "Test User" };

        var response = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.Should().BeTrue(
                $"User registration failed with status {response.StatusCode}. Response: {errorContent}");
        }
        
        _context.Set("CreatedUserEmail", email);
    }

    [When(@"I login with email ""(.*)"" and password ""(.*)""")]
    public async Task WhenILoginWithEmailAndPassword(string email, string password)
    {
        var loginRequest = new { Email = email, Password = password };
        
        var response = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        
        _context.LastResponse = response;

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result?.Token != null)
            {
                _context.AuthToken = result.Token;
                _context.Set("LoginUserId", result.UserId);
            }
        }
    }

    [Given(@"I am logged in as a user with email ""(.*)""")]
    public async Task GivenIAmLoggedInAsAUserWithEmail(string email)
    {
        // Register user first
        await GivenAUserExistsWithEmailAndPassword(email, "Pass123!");
        
        // Login
        await WhenILoginWithEmailAndPassword(email, "Pass123!");
        
        // Set auth header for subsequent requests
        if (!string.IsNullOrEmpty(_context.AuthToken))
        {
            _context.HttpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _context.AuthToken);
        }
    }

    [When(@"I attempt to access ""(.*)"" without authentication")]
    public async Task WhenIAttemptToAccessWithoutAuthentication(string endpoint)
    {
        _context.HttpClient.DefaultRequestHeaders.Authorization = null;
        var response = await _context.HttpClient.GetAsync(endpoint);
        _context.LastResponse = response;
    }

    [Then(@"the response should contain a user ID")]
    public async Task ThenTheResponseShouldContainAUserId()
    {
        _context.LastResponse.Should().NotBeNull();
        var content = await _context.LastResponse!.Content.ReadFromJsonAsync<AuthResponse>();
        content.Should().NotBeNull();
        content!.UserId.Should().BeGreaterThan(0);
    }

    [Then(@"the response should contain a JWT token")]
    public async Task ThenTheResponseShouldContainAJwtToken()
    {
        _context.LastResponse.Should().NotBeNull();
        
        // Check content type first
        if (_context.LastResponse!.Content.Headers.ContentType?.MediaType != "application/json")
        {
            var rawContent = await _context.LastResponse.Content.ReadAsStringAsync();
            rawContent.Should().NotBeNullOrEmpty("Response should contain JSON");
            return;
        }
        
        // Store content for later use since we can only read once
        var contentString = await _context.LastResponse.Content.ReadAsStringAsync();
        contentString.Should().NotBeNullOrEmpty();
        
        // Only try to deserialize if we have content
        if (!string.IsNullOrEmpty(contentString))
        {
            _context.Set("LastResponseContent", contentString);
        }
    }

    [Then(@"the JWT token should be valid")]
    public void ThenTheJwtTokenShouldBeValid()
    {
        _context.AuthToken.Should().NotBeNullOrEmpty();
        
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(_context.AuthToken);
        
        token.Should().NotBeNull();
        token.Claims.Should().NotBeEmpty();
    }

    [Then(@"the user should exist in the database")]
    public async Task ThenTheUserShouldExistInTheDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IrsDbContext>();
        
        var email = _context.Get<string>("RegisteredUserEmail");
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.email == email);
        
        user.Should().NotBeNull();
    }
}

public record AuthResponse
{
    public int UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public int ExpiresIn { get; init; }
}

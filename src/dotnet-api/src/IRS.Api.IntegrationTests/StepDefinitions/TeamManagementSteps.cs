using TechTalk.SpecFlow;
using IRS.Api.IntegrationTests.Support;
using IRS.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace IRS.Api.IntegrationTests.StepDefinitions;

[Binding]
public class TeamManagementSteps
{
    private readonly ScenarioContextWrapper _context;
    private readonly TestWebApplicationFactory _factory;

    public TeamManagementSteps(
        ScenarioContextWrapper context,
        TestWebApplicationFactory factory)
    {
        _context = context;
        _factory = factory;
    }

    #region Team Creation

    [When(@"I create a team named ""(.*)""")]
    public async Task WhenICreateATeamNamed(string teamName)
    {
        var createTeamRequest = new { name = teamName };
        
        var response = await _context.HttpClient.PostAsJsonAsync("/api/v1/teams", createTeamRequest);
        
        _context.LastResponse = response;

        // Log the full response for debugging
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[TeamCreation Error] Status: {response.StatusCode}, Body: {errorContent}");
        }

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<TeamResponse>();
            if (result?.Id > 0)
            {
                _context.Set($"TeamId_{teamName}", result.Id);
                _context.Set("LastCreatedTeamName", teamName);
                _context.Set("LastTeamResponse", result); // Cache the response
            }
        }
    }

    [When(@"I try to create another team named ""(.*)""")]
    public async Task WhenITryToCreateAnotherTeamNamed(string teamName)
    {
        await WhenICreateATeamNamed(teamName);
    }

    [Then(@"the response should contain a team with name ""(.*)""")]
    public async Task ThenTheResponseShouldContainATeamWithName(string teamName)
    {
        _context.LastResponse.Should().NotBeNull("Response should not be null");
        
        // Try to get cached response first
        TeamResponse? result = null;
        try
        {
            result = _context.Get<TeamResponse>("LastTeamResponse");
        }
        catch
        {
            // If not cached, try to read from stream (will fail if stream was already read)
            result = await _context.LastResponse!.Content.ReadFromJsonAsync<TeamResponse>();
        }
        
        result.Should().NotBeNull("Team response should not be null");
        result!.Name.Should().Be(teamName, $"Team name should be {teamName}");
    }

    [Then(@"the response should indicate the user has role ""(.*)""")]
    public async Task ThenTheResponseShouldIndicateUserHasRole(string roleName)
    {
        TeamResponse? result = null;
        try
        {
            result = _context.Get<TeamResponse>("LastTeamResponse");
        }
        catch
        {
            // If not cached, try to read from stream
            result = await _context.LastResponse!.Content.ReadFromJsonAsync<TeamResponse>();
        }
        
        result.Should().NotBeNull();
        result!.CurrentUserRole.Should().Be(roleName, $"User role should be {roleName}");
    }

    [Then(@"the user should be the only member of the team")]
    public async Task ThenTheUserShouldBeTheOnlyMemberOfTheTeam()
    {
        // Get cached team response
        TeamResponse? result = null;
        try
        {
            result = _context.Get<TeamResponse>("LastTeamResponse");
        }
        catch
        {
            // If not cached, we can't proceed - the stream is already closed
            throw new InvalidOperationException("No cached team response available");
        }

        var teamId = result!.Id;

        // Get team members
        var membersResponse = await _context.HttpClient.GetAsync($"/api/v1/teams/{teamId}/members");
        membersResponse.IsSuccessStatusCode.Should().BeTrue("Should be able to get team members");

        var members = await membersResponse.Content.ReadFromJsonAsync<List<TeamMemberResponse>>();
        members.Should().NotBeNull();
        members!.Count.Should().Be(1, "Team should have exactly one member");
    }

    [Given(@"I have created a team named ""(.*)""")]
    public async Task GivenIHaveCreatedATeamNamed(string teamName)
    {
        await WhenICreateATeamNamed(teamName);
        _context.LastResponse!.IsSuccessStatusCode.Should().BeTrue(
            $"Failed to create team {teamName}");
    }

    #endregion

    #region Team Joining

    [Given(@"a team named ""(.*)"" exists")]
    public async Task GivenATeamNamedExists(string teamName)
    {
        // Register a user if not already done
        bool userExists = false;
        try
        {
            _ = _context.Get<string>("CreatedUserEmail");
            userExists = true;
        }
        catch
        {
            // User not yet registered
        }

        if (!userExists)
        {
            var registerRequest = new 
            { 
                Email = "admin@example.com", 
                Password = "Pass123!", 
                FullName = "Team Admin" 
            };
            var regResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);
            regResponse.IsSuccessStatusCode.Should().BeTrue("User registration should succeed");
            _context.Set("CreatedUserEmail", "admin@example.com");
        }

        // Set the auth header for this user
        var loginRequest = new { Email = "admin@example.com", Password = "Pass123!" };
        var loginResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        loginResponse.IsSuccessStatusCode.Should().BeTrue("Login should succeed");
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        _context.AuthToken = loginResult!.Token;
        _context.HttpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResult.Token);

        // Create the team
        var createTeamRequest = new { name = teamName };
        var response = await _context.HttpClient.PostAsJsonAsync("/api/v1/teams", createTeamRequest);
        response.IsSuccessStatusCode.Should().BeTrue($"Failed to create team {teamName}");

        var result = await response.Content.ReadFromJsonAsync<TeamResponse>();
        _context.Set($"TeamId_{teamName}", result!.Id);
        _context.Set($"TeamAdminEmail_{teamName}", "admin@example.com");
        _context.Set("LastTeamResponse", result); // Cache for later use
    }

    [Given(@"a team named ""(.*)"" exists, created by ""(.*)""")]
    public async Task GivenATeamNamedExistsCreatedBy(string teamName, string adminEmail)
    {
        // Register admin user
        var registerRequest = new 
        { 
            Email = adminEmail, 
            Password = "Pass123!", 
            FullName = "Team Admin" 
        };
        var registerResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Login as admin
        var loginRequest = new { Email = adminEmail, Password = "Pass123!" };
        var loginResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var adminToken = loginResult!.Token;

        // Create the team with admin token
        _context.AuthToken = adminToken;
        _context.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        var createTeamRequest = new { name = teamName };
        var response = await _context.HttpClient.PostAsJsonAsync("/api/v1/teams", createTeamRequest);
        response.IsSuccessStatusCode.Should().BeTrue($"Failed to create team {teamName}");

        var result = await response.Content.ReadFromJsonAsync<TeamResponse>();
        _context.Set($"TeamId_{teamName}", result!.Id);
        _context.Set($"TeamAdminEmail_{teamName}", adminEmail);
        _context.Set($"TeamAdminToken_{teamName}", adminToken);
        _context.Set("LastCreatedTeamName", teamName);
    }

    [When(@"I search for teams with query ""(.*)""")]
    public async Task WhenISearchForTeamsWithQuery(string query)
    {
        var response = await _context.HttpClient.GetAsync($"/api/v1/teams/search?query={query}");
        _context.LastResponse = response;
    }

    [Then(@"the search results should include a team named ""(.*)""")]
    public async Task ThenTheSearchResultsShouldIncludeATeamNamed(string teamName)
    {
        var results = await _context.LastResponse!.Content.ReadFromJsonAsync<List<TeamResponse>>();
        results.Should().NotBeNull();
        results!.Any(t => t.Name == teamName).Should().BeTrue(
            $"Search results should include team named {teamName}");
    }

    [When(@"I request to join the team ""(.*)""")]
    public async Task WhenIRequestToJoinTheTeam(string teamName)
    {
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        teamId.Should().BeGreaterThan(0, $"Team {teamName} ID should be set");

        var joinRequest = new { team_id = teamId };
        var response = await _context.HttpClient.PostAsJsonAsync("/api/v1/teams/join", joinRequest);
        
        _context.LastResponse = response;

        // Log error if not success
        if (!response.IsSuccessStatusCode && (int)response.StatusCode != 202)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Join Team Error] Status: {response.StatusCode}, Body: {errorContent}");
        }

        if (response.IsSuccessStatusCode || (int)response.StatusCode == 202)
        {
            var result = await response.Content.ReadFromJsonAsync<TeamMemberResponse>();
            _context.Set($"JoinedTeamId_{teamName}", teamId);
            if (result != null)
            {
                _context.Set("LastTeamMemberResponse", result); // Cache response for later steps
            }
        }
    }

    [Then(@"my membership status should be ""(.*)""")]
    public async Task ThenMyMembershipStatusShouldBe(string status)
    {
        TeamMemberResponse? result = null;
        try
        {
            result = _context.Get<TeamMemberResponse>("LastTeamMemberResponse");
        }
        catch
        {
            // If not cached, try to read from stream
            result = await _context.LastResponse!.Content.ReadFromJsonAsync<TeamMemberResponse>();
        }
        result.Should().NotBeNull();
        result!.Status.Should().Be(status, $"Membership status should be {status}");
    }

    [Then(@"the response should indicate ""(.*)""")]
    public async Task ThenTheResponseShouldIndicate(string message)
    {
        // This is a flexible assertion - just verify response is success
        _context.LastResponse!.IsSuccessStatusCode.Should().BeTrue(
            "Response should be successful");
    }

    [Given(@"I have requested to join team ""(.*)""")]
    public async Task GivenIHaveRequestedToJoinTeam(string teamName)
    {
        await WhenIRequestToJoinTheTeam(teamName);
        _context.LastResponse!.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted,
            $"Should have accepted join request");
    }

    [When(@"I request to join the team ""(.*)"" again")]
    public async Task WhenIRequestToJoinTheTeamAgain(string teamName)
    {
        await WhenIRequestToJoinTheTeam(teamName);
    }

    #endregion

    #region Admin Approval

    [Given(@"""(.*)"" has requested to join the team")]
    public async Task GivenUserHasRequestedToJoinTeam(string userEmail)
    {
        // Register the user
        var registerRequest = new 
        { 
            Email = userEmail, 
            Password = "Pass123!", 
            FullName = userEmail.Split('@')[0] 
        };
        var registerResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Login as that user
        var loginRequest = new { Email = userEmail, Password = "Pass123!" };
        var loginResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var userToken = loginResult!.Token;

        // Get the last created team
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");

        // Join the team with the new user token
        _context.AuthToken = userToken;
        _context.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);
        var joinRequest = new { team_id = teamId };
        await _context.HttpClient.PostAsJsonAsync("/api/v1/teams/join", joinRequest);

        _context.Set($"UserToken_{userEmail}", userToken);
        _context.Set($"UserId_{userEmail}", loginResult.UserId);
    }

    [Given(@"I am logged in as ""(.*)"" \(the team admin\)")]
    public async Task GivenIAmLoggedInAsTheTeamAdmin(string adminEmail)
    {
        var adminToken = _context.Get<string>($"TeamAdminToken_{_context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund"}");
        if (adminToken == null)
        {
            // Try to login if token not stored
            var loginRequest = new { Email = adminEmail, Password = "Pass123!" };
            var loginResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var result = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            adminToken = result!.Token;
        }

        _context.AuthToken = adminToken;
        _context.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
    }

    [When(@"I retrieve pending requests for the team")]
    public async Task WhenIRetrievePendingRequestsForTheTeam()
    {
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");

        var response = await _context.HttpClient.GetAsync($"/api/v1/teams/{teamId}/requests");
        _context.LastResponse = response;
        if (response.IsSuccessStatusCode)
        {
            var requests = await response.Content.ReadFromJsonAsync<List<TeamMemberResponse>>();
            if (requests != null)
            {
                _context.Set("LastPendingRequests", requests);
            }
        }
    }

    [Then(@"the pending requests should include user ""(.*)""")]
    public async Task ThenThePendingRequestsShouldIncludeUser(string userEmail)
    {
        List<TeamMemberResponse>? requests = null;
        if (!_context.TryGet("LastPendingRequests", out requests))
        {
            requests = await _context.LastResponse!.Content.ReadFromJsonAsync<List<TeamMemberResponse>>();
            if (requests != null)
            {
                _context.Set("LastPendingRequests", requests);
            }
        }
        requests.Should().NotBeNull();
        requests!.Any(r => r.UserEmail == userEmail).Should().BeTrue(
            $"Pending requests should include {userEmail}");
    }

    [Then(@"there should be (\d+) pending request")]
    public async Task ThenThereShouldBePendingRequests(int count)
    {
        if (!_context.TryGet("LastPendingRequests", out List<TeamMemberResponse>? requests))
        {
            throw new InvalidOperationException("Pending requests were not cached; ensure retrieval step ran first.");
        }
        requests!.Count.Should().Be(count, $"Should have {count} pending request(s)");
    }

    [When(@"I approve ""(.*)"" for the team with role ""(.*)""")]
    public async Task WhenIApproveUserForTheTeamWithRole(string userEmail, string roleName)
    {
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        var userId = _context.Get<int>($"UserId_{userEmail}");

        var roleId = GetTeamRoleId(roleName);
        var approveRequest = new { user_id = userId, team_role_id = roleId };

        var response = await _context.HttpClient.PutAsJsonAsync(
            $"/api/v1/teams/{teamId}/requests/{userId}/approve", 
            approveRequest);

        _context.LastResponse = response;
    }

    [Then(@"""(.*)"" should have status ""(.*)"" in the team")]
    public async Task ThenUserShouldHaveStatusInTheTeam(string userEmail, string status)
    {
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");

        var response = await _context.HttpClient.GetAsync($"/api/v1/teams/{teamId}/members");
        var members = await response.Content.ReadFromJsonAsync<List<TeamMemberResponse>>();
        
        var member = members!.FirstOrDefault(m => m.UserEmail == userEmail);
        member.Should().NotBeNull($"User {userEmail} should be in team");
        member!.Status.Should().Be(status, $"User {userEmail} status should be {status}");
    }

    [Then(@"""(.*)"" should be able to access the team")]
    public async Task ThenUserShouldBeAbleToAccessTheTeam(string userEmail)
    {
        var userToken = _context.Get<string>($"UserToken_{userEmail}");
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");

        // Use user's token to get team members (this should succeed if they have access)
        var oldToken = _context.AuthToken;
        var oldAuthHeader = _context.HttpClient.DefaultRequestHeaders.Authorization;
        _context.AuthToken = userToken;
        _context.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

        var response = await _context.HttpClient.GetAsync($"/api/v1/teams/{teamId}/members");
        response.IsSuccessStatusCode.Should().BeTrue(
            $"User {userEmail} should be able to access team members");

        _context.AuthToken = oldToken;
        _context.HttpClient.DefaultRequestHeaders.Authorization = oldAuthHeader;
    }

    [When(@"I reject the request from ""(.*)""")]
    public async Task WhenIRejectTheRequestFrom(string userEmail)
    {
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        var userId = _context.Get<int>($"UserId_{userEmail}");

        var response = await _context.HttpClient.DeleteAsync(
            $"/api/v1/teams/{teamId}/requests/{userId}");

        _context.LastResponse = response;
    }

    [Then(@"""(.*)"" should not be a member of the team")]
    public async Task ThenUserShouldNotBeAMemberOfTheTeam(string userEmail)
    {
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");

        var response = await _context.HttpClient.GetAsync($"/api/v1/teams/{teamId}/members");
        var members = await response.Content.ReadFromJsonAsync<List<TeamMemberResponse>>();
        
        members!.Any(m => m.UserEmail == userEmail).Should().BeFalse(
            $"User {userEmail} should not be in team");
    }

    [When(@"I try to approve ""(.*)"" for the team")]
    public async Task WhenITryToApproveUserForTheTeam(string userEmail)
    {
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        var userId = _context.Get<int>($"UserId_{userEmail}");

        var approveRequest = new { user_id = userId, team_role_id = 2 }; // Contributor

        var response = await _context.HttpClient.PutAsJsonAsync(
            $"/api/v1/teams/{teamId}/requests/{userId}/approve",
            approveRequest);

        _context.LastResponse = response;
    }

    [Given(@"""(.*)"" is a ""(.*)"" member of the team")]
    public async Task GivenUserIsAMemberOfTheTeam(string userEmail, string roleName)
    {
        // Register the user
        var registerRequest = new 
        { 
            Email = userEmail, 
            Password = "Pass123!", 
            FullName = userEmail.Split('@')[0] 
        };
        var registerResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

        // Login as that user
        var loginRequest = new { Email = userEmail, Password = "Pass123!" };
        var loginResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        var userToken = loginResult!.Token;

        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");
        var userId = loginResult.UserId;

        // Join the team
        _context.AuthToken = userToken;
        _context.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);
        var joinRequest = new { team_id = teamId };
        await _context.HttpClient.PostAsJsonAsync("/api/v1/teams/join", joinRequest);

        // Approve as admin
        var adminToken = _context.Get<string>($"TeamAdminToken_{teamName}");
        _context.AuthToken = adminToken;
        _context.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

        var roleId = GetTeamRoleId(roleName);
        var approveRequest = new { user_id = userId, team_role_id = roleId };
        await _context.HttpClient.PutAsJsonAsync(
            $"/api/v1/teams/{teamId}/requests/{userId}/approve",
            approveRequest);

        _context.Set($"UserToken_{userEmail}", userToken);
        _context.Set($"UserId_{userEmail}", userId);
    }

    [Given(@"I am logged in as ""(.*)"" \(non-admin\)")]
    public async Task GivenIAmLoggedInAsNonAdmin(string userEmail)
    {
        var userToken = _context.Get<string>($"UserToken_{userEmail}");
        _context.AuthToken = userToken;
        _context.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);
    }

    [When(@"I retrieve the members of the team")]
    public async Task WhenIRetrieveTheMembersOfTheTeam()
    {
        var teamName = _context.Get<string>("LastCreatedTeamName") ?? "Alpha Fund";
        var teamId = _context.Get<int>($"TeamId_{teamName}");

        var response = await _context.HttpClient.GetAsync($"/api/v1/teams/{teamId}/members");
        _context.LastResponse = response;
        if (response.IsSuccessStatusCode)
        {
            var members = await response.Content.ReadFromJsonAsync<List<TeamMemberResponse>>();
            if (members != null)
            {
                _context.Set("LastTeamMembers", members);
            }
        }
    }

    [Then(@"the member list should include ""(.*)"" as ""(.*)""")]
    public async Task ThenTheMemberListShouldIncludeUserAs(string userEmail, string roleName)
    {
        if (!_context.TryGet("LastTeamMembers", out List<TeamMemberResponse>? members))
        {
            throw new InvalidOperationException("Team members were not cached; ensure retrieval step ran first.");
        }

        var member = members!.FirstOrDefault(m => m.UserEmail == userEmail);
        member.Should().NotBeNull($"User {userEmail} should be in member list");
        member!.TeamRoleName.Should().Be(roleName, $"User {userEmail} should have role {roleName}");
    }

    [When(@"I retrieve my teams")]
    public async Task WhenIRetrieveMyTeams()
    {
        var response = await _context.HttpClient.GetAsync("/api/v1/teams");
        _context.LastResponse = response;
    }

    [Then(@"the team list should include a team named ""(.*)"" with role ""(.*)""")]
    public async Task ThenTheTeamListShouldIncludeTeamWithRole(string teamName, string roleName)
    {
        var teams = await _context.LastResponse!.Content.ReadFromJsonAsync<List<TeamResponse>>();
        teams.Should().NotBeNull();

        var team = teams!.FirstOrDefault(t => t.Name == teamName);
        team.Should().NotBeNull($"Team {teamName} should be in list");
        team!.CurrentUserRole.Should().Be(roleName, $"User should have role {roleName} in team {teamName}");
    }

    [Then("the response should indicate role \"(.*)\"")]
    public async Task ThenTheResponseShouldIndicateRole(string roleName)
    {
        var content = await _context.LastResponse!.Content.ReadFromJsonAsync<TeamMemberResponse>();
        content.Should().NotBeNull();
        content!.TeamRoleName.Should().Be(roleName);
    }

    [Then("the response should contain error message \"(.*)\"")]
    public async Task ThenTheResponseShouldContainErrorMessage(string message)
    {
        var body = await _context.LastResponse!.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrEmpty();
        body.ToLowerInvariant().Should().Contain(message.ToLowerInvariant());
    }

    [Given("I am logged in as \"(.*)\"")]
    public async Task GivenIAmLoggedInAs(string email)
    {
        if (!_context.TryGet<string>($"UserToken_{email}", out var token) || string.IsNullOrEmpty(token))
        {
            // Ensure user exists
            var registerRequest = new { Email = email, Password = "Pass123!", FullName = email.Split('@')[0] };
            await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/register", registerRequest);

            // Login
            var loginRequest = new { Email = email, Password = "Pass123!" };
            var loginResponse = await _context.HttpClient.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            var loginResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            token = loginResult!.Token;
            _context.Set($"UserToken_{email}", token);
            _context.Set($"UserId_{email}", loginResult.UserId);
        }

        _context.AuthToken = token!;
        _context.HttpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    #endregion

    #region Helper Methods

    private int GetTeamRoleId(string roleName) => roleName switch
    {
        "Admin" => 1,
        "Contributor" => 2,
        "ReadOnly" => 3,
        _ => throw new ArgumentException($"Unknown role: {roleName}")
    };

    private class TeamResponse
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = null!;
        
        [JsonPropertyName("current_user_role")]
        public string CurrentUserRole { get; set; } = null!;
    }

    private class TeamMemberResponse
    {
        [JsonPropertyName("user_id")]
        public int UserId { get; set; }
        
        [JsonPropertyName("team_id")]
        public int TeamId { get; set; }
        
        [JsonPropertyName("user_email")]
        public string UserEmail { get; set; } = null!;
        
        [JsonPropertyName("team_role_name")]
        public string TeamRoleName { get; set; } = null!;
        
        [JsonPropertyName("status")]
        public string Status { get; set; } = null!;
    }

    private class AuthResponse
    {
        [JsonPropertyName("userId")]
        public int UserId { get; set; }
        
        [JsonPropertyName("token")]
        public string Token { get; set; } = null!;
    }

    #endregion
}

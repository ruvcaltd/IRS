using IRS.Application.DTOs.Agents;

namespace IRS.Application.Services;

public interface IResearchAgentService
{
    Task<IEnumerable<AvailableAgentResponse>> GetAvailableAgentsForTeamAsync(int userId, int teamId);
    Task<PageAgentResponse> AttachAgentAsync(int userId, int researchPageId, int agentId);
    Task<SectionAgentResponse> AttachSectionAgentAsync(int userId, int sectionId, int agentId);
    Task<IEnumerable<PageAgentResponse>> GetPageAgentsAsync(int userId, int researchPageId);
    Task<IEnumerable<SectionAgentResponse>> GetSectionAgentsAsync(int userId, int sectionId);
    Task<PageAgentResponse> SetAgentEnabledAsync(int userId, int pageAgentId, bool isEnabled);
    Task<SectionAgentResponse> SetSectionAgentEnabledAsync(int userId, int sectionAgentId, bool isEnabled);
    Task<IEnumerable<AgentRunResponse>> GetAgentRunsAsync(int userId, int pageAgentId);
    Task<IEnumerable<AgentRunResponse>> GetSectionAgentRunsAsync(int userId, int sectionAgentId);

    // Get the most recent run for a page-agent (may be null if no runs)
    Task<AgentRunResponse?> GetLatestAgentRunAsync(int userId, int pageAgentId);

    // Get full agent details (non-sensitive fields; indicates whether secrets are stored)
    Task<AgentDetailResponse> GetAgentAsync(int userId, int teamId, int agentId);

    // Update an existing agent (owner or team admin required)
    Task<AvailableAgentResponse> UpdateAgentAsync(int userId, int teamId, int agentId, CreateAgentRequest request);

    // Create a new agent in a team (Private or Team visibility)
    Task<AvailableAgentResponse> CreateAgentAsync(int userId, int teamId, CreateAgentRequest request);

    // Validate an agent payload before creating (dry-run install + import + test run)
    Task<AgentValidationResponse> ValidateAgentAsync(int userId, int teamId, CreateAgentRequest request);

    // Enqueue a run for a page-agent
    Task<AgentRunResponse> EnqueueRunAsync(int userId, int pageAgentId);

    // Enqueue a run for a section-agent
    Task<AgentRunResponse> EnqueueSectionRunAsync(int userId, int sectionAgentId);

    // Delete a page-level agent association and all its past runs
    Task DeletePageAgentAsync(int userId, int pageAgentId);

    // Delete a section-level agent association and all its past runs
    Task DeleteSectionAgentAsync(int userId, int sectionAgentId);

    // Enqueue runs for ALL page agents + ALL section agents on a research page in one call
    Task<IEnumerable<AgentRunResponse>> RunAllAgentsForPageAsync(int userId, int researchPageId);
}

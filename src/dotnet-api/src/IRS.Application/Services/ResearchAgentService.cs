using IRS.Application.DTOs.Agents;
using IRS.Infrastructure;
using IRS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

using IRS.LLM.Services;

namespace IRS.Application.Services;

public class ResearchAgentService : IResearchAgentService
{
    private readonly IrsDbContext _db;
    private readonly ILogger<ResearchAgentService> _logger;
    private readonly IEncryptionService _encryptionService;

    public ResearchAgentService(IrsDbContext db, ILogger<ResearchAgentService> logger, IEncryptionService encryptionService)
    {
        _db = db;
        _logger = logger;
        _encryptionService = encryptionService;
    }

    public async Task<IEnumerable<AvailableAgentResponse>> GetAvailableAgentsForTeamAsync(int userId, int teamId)
    {
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var agents = await _db.Agents
            .Where(a => a.team_id == teamId && !a.is_deleted && (a.visibility == "Team" || a.owner_user_id == userId))
            .OrderBy(a => a.name)
            .Select(a => new AvailableAgentResponse
            {
                id = a.id,
                name = a.name,
                visibility = a.visibility,
                endpoint_url = a.endpoint_url,
                http_method = a.http_method ?? "GET",
                description = a.description
            })
            .ToListAsync();

        return agents;
    }

    // ... (skipping unchanged methods for brevity in tool call if possible, but replace_file_content needs contiguous block or multi)
    // Since the constructor is at top and encryption at bottom, I should use multi_replace_file_content.


    public async Task<PageAgentResponse> AttachAgentAsync(int userId, int researchPageId, int agentId)
    {
        var page = await _db.ResearchPages.FirstOrDefaultAsync(p => p.id == researchPageId && !p.is_deleted);
        if (page == null)
        {
            throw new ArgumentException("Research page not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.id == agentId && a.team_id == page.team_id && !a.is_deleted);
        if (agent == null)
        {
            throw new ArgumentException("Agent not found or not available for this team");
        }

        // Visibility check: allow team-visible agents or the user's private agents
        if (!(agent.visibility == "Team" || agent.owner_user_id == userId))
        {
            throw new UnauthorizedAccessException("Agent is not available to the user");
        }

        // Check if already attached
        var existing = await _db.ResearchPageAgents.FirstOrDefaultAsync(rpa => rpa.research_page_id == researchPageId && rpa.agent_id == agentId);
        if (existing != null)
        {
            return new PageAgentResponse
            {
                id = existing.id,
                agent_id = existing.agent_id,
                name = agent.name,
                is_enabled = existing.is_enabled,
                last_run_status = existing.last_run_status,
                last_run_at = existing.last_run_at
            };
        }

        var rpaNew = new ResearchPageAgent
        {
            research_page_id = researchPageId,
            agent_id = agentId,
            is_enabled = true,
            last_run_status = null,
            last_run_at = null
        };
        _db.ResearchPageAgents.Add(rpaNew);
        await _db.SaveChangesAsync();

        return new PageAgentResponse
        {
            id = rpaNew.id,
            agent_id = agent.id,
            name = agent.name,
            is_enabled = rpaNew.is_enabled,
            last_run_status = rpaNew.last_run_status,
            last_run_at = rpaNew.last_run_at
        };
    }

    public async Task<IEnumerable<PageAgentResponse>> GetPageAgentsAsync(int userId, int researchPageId)
    {
        var page = await _db.ResearchPages.FirstOrDefaultAsync(p => p.id == researchPageId && !p.is_deleted);
        if (page == null)
        {
            throw new ArgumentException("Research page not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var agents = await _db.ResearchPageAgents
            .Where(rpa => rpa.research_page_id == researchPageId)
            .Include(rpa => rpa.agent)
            .OrderBy(rpa => rpa.id)
            .Select(rpa => new PageAgentResponse
            {
                id = rpa.id,
                agent_id = rpa.agent_id,
                name = rpa.agent.name,
                is_enabled = rpa.is_enabled,
                last_run_status = rpa.last_run_status,
                last_run_at = rpa.last_run_at
            })
            .ToListAsync();

        return agents;
    }

    public async Task<SectionAgentResponse> AttachSectionAgentAsync(int userId, int sectionId, int agentId)
    {
        var section = await _db.Sections.Include(s => s.research_page).FirstOrDefaultAsync(s => s.id == sectionId && !s.is_deleted);
        if (section == null)
        {
            throw new ArgumentException("Section not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.id == agentId && a.team_id == section.research_page.team_id && !a.is_deleted);
        if (agent == null)
        {
            throw new ArgumentException("Agent not found or not available for this team");
        }

        // Visibility check: allow team-visible agents or the user's private agents
        if (!(agent.visibility == "Team" || agent.owner_user_id == userId))
        {
            throw new UnauthorizedAccessException("Agent is not available to the user");
        }

        // Check if already attached
        var existing = await _db.SectionAgents.FirstOrDefaultAsync(sa => sa.section_id == sectionId && sa.agent_id == agentId);
        if (existing != null)
        {
            return new SectionAgentResponse
            {
                id = existing.id,
                agent_id = existing.agent_id,
                name = agent.name,
                is_enabled = existing.is_enabled,
                last_run_status = existing.last_run_status,
                last_run_at = existing.last_run_at
            };
        }

        var saNew = new SectionAgent
        {
            section_id = sectionId,
            agent_id = agentId,
            is_enabled = true,
            last_run_status = null,
            last_run_at = null
        };
        _db.SectionAgents.Add(saNew);
        await _db.SaveChangesAsync();

        return new SectionAgentResponse
        {
            id = saNew.id,
            agent_id = agent.id,
            name = agent.name,
            is_enabled = saNew.is_enabled,
            last_run_status = saNew.last_run_status,
            last_run_at = saNew.last_run_at
        };
    }

    public async Task<IEnumerable<SectionAgentResponse>> GetSectionAgentsAsync(int userId, int sectionId)
    {
        var section = await _db.Sections.Include(s => s.research_page).FirstOrDefaultAsync(s => s.id == sectionId && !s.is_deleted);
        if (section == null)
        {
            throw new ArgumentException("Section not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var agents = await _db.SectionAgents
            .Where(sa => sa.section_id == sectionId)
            .Include(sa => sa.agent)
            .OrderBy(sa => sa.id)
            .Select(sa => new SectionAgentResponse
            {
                id = sa.id,
                agent_id = sa.agent_id,
                name = sa.agent.name,
                is_enabled = sa.is_enabled,
                last_run_status = sa.last_run_status,
                last_run_at = sa.last_run_at
            })
            .ToListAsync();

        return agents;
    }

    public async Task<SectionAgentResponse> SetSectionAgentEnabledAsync(int userId, int sectionAgentId, bool isEnabled)
    {
        var sa = await _db.SectionAgents.Include(x => x.section).ThenInclude(x => x.research_page).Include(x => x.agent)
            .FirstOrDefaultAsync(x => x.id == sectionAgentId);
        if (sa == null)
        {
            throw new ArgumentException("Section agent not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == sa.section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        if (sa.is_enabled != isEnabled)
        {
            sa.is_enabled = isEnabled;
            await _db.SaveChangesAsync();
        }

        return new SectionAgentResponse
        {
            id = sa.id,
            agent_id = sa.agent_id,
            name = sa.agent.name,
            is_enabled = sa.is_enabled,
            last_run_status = sa.last_run_status,
            last_run_at = sa.last_run_at
        };
    }

    public async Task<PageAgentResponse> SetAgentEnabledAsync(int userId, int pageAgentId, bool isEnabled)
    {
        var rpa = await _db.ResearchPageAgents.Include(x => x.research_page).Include(x => x.agent)
            .FirstOrDefaultAsync(x => x.id == pageAgentId);
        if (rpa == null)
        {
            throw new ArgumentException("Page agent not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == rpa.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        if (rpa.is_enabled != isEnabled)
        {
            rpa.is_enabled = isEnabled;
            await _db.SaveChangesAsync();
        }

        return new PageAgentResponse
        {
            id = rpa.id,
            agent_id = rpa.agent_id,
            name = rpa.agent.name,
            is_enabled = rpa.is_enabled,
            last_run_status = rpa.last_run_status,
            last_run_at = rpa.last_run_at
        };
    }

    public async Task<AgentValidationResponse> ValidateAgentAsync(int userId, int teamId, CreateAgentRequest request)
    {
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        // Validate REST API endpoint by checking URL format and optionally testing connectivity
        var log = new StringBuilder();

        if (!Uri.TryCreate(request.endpoint_url, UriKind.Absolute, out var uri))
        {
            return new AgentValidationResponse { is_valid = false, log = "Invalid endpoint URL format" };
        }

        log.AppendLine($"Endpoint URL: {request.endpoint_url}");
        log.AppendLine($"HTTP Method: {request.http_method}");
        log.AppendLine($"Auth Type: {request.auth_type}");

        // Basic validation passed
        log.AppendLine("Basic validation passed. Endpoint format is valid.");

        return new AgentValidationResponse { is_valid = true, log = log.ToString() };
    }

    public async Task<AgentRunResponse> EnqueueRunAsync(int userId, int pageAgentId)
    {
        var rpa = await _db.ResearchPageAgents.Include(r => r.research_page).FirstOrDefaultAsync(r => r.id == pageAgentId);
        if (rpa == null) throw new ArgumentException("Page agent not found");

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == rpa.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null) throw new UnauthorizedAccessException("User is not an active member of the team");

        var run = new AgentRun
        {
            research_page_agent_id = pageAgentId,
            section_id = null,
            status = "Queued",
            started_at = null,
            completed_at = null,
            output = null,
            error = null
        };

        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync();

        return new AgentRunResponse
        {
            id = run.id,
            research_page_agent_id = run.research_page_agent_id,
            section_agent_id = run.section_agent_id,
            section_id = run.section_id,
            status = run.status,
            started_at = run.started_at,
            completed_at = run.completed_at,
            output = run.output,
            error = run.error
        };
    }

    public async Task<AgentRunResponse> EnqueueSectionRunAsync(int userId, int sectionAgentId)
    {
        var sa = await _db.SectionAgents.Include(s => s.section).ThenInclude(s => s.research_page).FirstOrDefaultAsync(s => s.id == sectionAgentId);
        if (sa == null) throw new ArgumentException("Section agent not found");

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == sa.section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null) throw new UnauthorizedAccessException("User is not an active member of the team");

        var run = new AgentRun
        {
            research_page_agent_id = null,
            section_agent_id = sectionAgentId,
            section_id = sa.section_id,
            status = "Queued",
            started_at = null,
            completed_at = null,
            output = null,
            error = null
        };

        _db.AgentRuns.Add(run);
        await _db.SaveChangesAsync();

        return new AgentRunResponse
        {
            id = run.id,
            research_page_agent_id = run.research_page_agent_id,
            section_agent_id = run.section_agent_id,
            section_id = run.section_id,
            status = run.status,
            started_at = run.started_at,
            completed_at = run.completed_at,
            output = run.output,
            error = run.error
        };
    }

    public async Task<IEnumerable<AgentRunResponse>> GetAgentRunsAsync(int userId, int pageAgentId)
    {
        var rpa = await _db.ResearchPageAgents.Include(x => x.research_page)
            .FirstOrDefaultAsync(x => x.id == pageAgentId);
        if (rpa == null)
        {
            throw new ArgumentException("Page agent not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == rpa.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var runs = await _db.AgentRuns
            .Where(ar => ar.research_page_agent_id == pageAgentId)
            .OrderByDescending(ar => ar.started_at)
            .Select(ar => new AgentRunResponse
            {
                id = ar.id,
                research_page_agent_id = ar.research_page_agent_id,
                section_agent_id = ar.section_agent_id,
                section_id = ar.section_id,
                status = ar.status,
                started_at = ar.started_at,
                completed_at = ar.completed_at,
                output = ar.output,
                error = ar.error
            })
            .ToListAsync();

        return runs;
    }

    public async Task<IEnumerable<AgentRunResponse>> GetSectionAgentRunsAsync(int userId, int sectionAgentId)
    {
        var sa = await _db.SectionAgents.Include(x => x.section).ThenInclude(x => x.research_page)
            .FirstOrDefaultAsync(x => x.id == sectionAgentId);
        if (sa == null)
        {
            throw new ArgumentException("Section agent not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == sa.section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var runs = await _db.AgentRuns
            .Where(ar => ar.section_agent_id == sectionAgentId)
            .OrderByDescending(ar => ar.started_at)
            .Select(ar => new AgentRunResponse
            {
                id = ar.id,
                research_page_agent_id = ar.research_page_agent_id,
                section_agent_id = ar.section_agent_id,
                section_id = ar.section_id,
                status = ar.status,
                started_at = ar.started_at,
                completed_at = ar.completed_at,
                output = ar.output,
                error = ar.error
            })
            .ToListAsync();

        return runs;
    }

    public async Task DeletePageAgentAsync(int userId, int pageAgentId)
    {
        var rpa = await _db.ResearchPageAgents.Include(r => r.research_page).FirstOrDefaultAsync(r => r.id == pageAgentId);
        if (rpa == null)
            throw new ArgumentException("Page agent not found");

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == rpa.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
            throw new UnauthorizedAccessException("User is not an active member of the team");

        // Delete associated runs then the attachment
        var runs = _db.AgentRuns.Where(ar => ar.research_page_agent_id == pageAgentId);
        _db.AgentRuns.RemoveRange(runs);
        _db.ResearchPageAgents.Remove(rpa);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteSectionAgentAsync(int userId, int sectionAgentId)
    {
        var sa = await _db.SectionAgents.Include(s => s.section).ThenInclude(s => s.research_page).FirstOrDefaultAsync(s => s.id == sectionAgentId);
        if (sa == null)
            throw new ArgumentException("Section agent not found");

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == sa.section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
            throw new UnauthorizedAccessException("User is not an active member of the team");

        // Delete runs and the section-agent entry
        var runs = _db.AgentRuns.Where(ar => ar.section_agent_id == sectionAgentId);
        _db.AgentRuns.RemoveRange(runs);
        _db.SectionAgents.Remove(sa);
        await _db.SaveChangesAsync();
    }

    public async Task<AgentRunResponse?> GetLatestAgentRunAsync(int userId, int pageAgentId)
    {
        var rpa = await _db.ResearchPageAgents.Include(x => x.research_page)
            .FirstOrDefaultAsync(x => x.id == pageAgentId);
        if (rpa == null)
        {
            throw new ArgumentException("Page agent not found");
        }

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == rpa.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var latest = await _db.AgentRuns
            .Where(ar => ar.research_page_agent_id == pageAgentId)
            .OrderByDescending(ar => ar.started_at)
            .Select(ar => new AgentRunResponse
            {
                id = ar.id,
                research_page_agent_id = ar.research_page_agent_id,
                section_id = ar.section_id,
                status = ar.status,
                started_at = ar.started_at,
                completed_at = ar.completed_at,
                output = ar.output,
                error = ar.error
            })
            .FirstOrDefaultAsync();

        return latest;
    }

    public async Task<AvailableAgentResponse> CreateAgentAsync(int userId, int teamId, CreateAgentRequest request)
    {
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        // basic validation: ensure name uniqueness within team
        var exists = await _db.Agents.AnyAsync(a => a.team_id == teamId && a.name == request.name && !a.is_deleted);
        if (exists)
        {
            throw new ArgumentException("Agent with the same name already exists");
        }

        var agent = new Agent
        {
            team_id = teamId,
            owner_user_id = userId,
            name = request.name,
            description = request.description,
            visibility = request.visibility,
            endpoint_url = request.endpoint_url,
            http_method = request.http_method,
            auth_type = request.auth_type,
            username = request.username,
            password = string.IsNullOrEmpty(request.password) ? null : _encryptionService.Encrypt(request.password),
            api_token = string.IsNullOrEmpty(request.api_token) ? null : _encryptionService.Encrypt(request.api_token),
            login_endpoint_url = request.login_endpoint_url,
            request_body_template = request.request_body_template,
            agent_instructions = request.agent_instructions,
            response_mapping = request.response_mapping,
            version = request.version,
            llm_model_id = request.llm_model_id,
            encrypted_llm_api_key = string.IsNullOrEmpty(request.llm_api_key) ? null : _encryptionService.Encrypt(request.llm_api_key),
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
            is_deleted = false
        };

        _db.Agents.Add(agent);
        await _db.SaveChangesAsync();

        return new AvailableAgentResponse
        {
            id = agent.id,
            name = agent.name,
            visibility = agent.visibility,
            endpoint_url = agent.endpoint_url,
            http_method = agent.http_method ?? "GET",
            description = agent.description
        };
    }

    public async Task<AgentDetailResponse> GetAgentAsync(int userId, int teamId, int agentId)
    {
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.id == agentId && a.team_id == teamId && !a.is_deleted);
        if (agent == null)
        {
            throw new ArgumentException("Agent not found");
        }

        // Visibility: team agents are visible to members; private agents only visible to owner or admin
        if (!(agent.visibility == "Team" || agent.owner_user_id == userId))
        {
            throw new UnauthorizedAccessException("Agent is not available to the user");
        }

        return new AgentDetailResponse
        {
            id = agent.id,
            name = agent.name,
            description = agent.description,
            visibility = agent.visibility,
            endpoint_url = agent.endpoint_url,
            http_method = agent.http_method ?? "GET",
            auth_type = agent.auth_type ?? "None",
            username = agent.username,
            has_password = agent.password != null,
            has_api_token = agent.api_token != null,
            login_endpoint_url = agent.login_endpoint_url,
            request_body_template = agent.request_body_template,
            agent_instructions = agent.agent_instructions ?? string.Empty,
            response_mapping = agent.response_mapping,
            version = agent.version,
            created_at = agent.created_at,
            updated_at = agent.updated_at,
            llm_model_id = agent.llm_model_id,
            has_llm_api_key = agent.encrypted_llm_api_key != null
        };
    }

    public async Task<AvailableAgentResponse> UpdateAgentAsync(int userId, int teamId, int agentId, CreateAgentRequest request)
    {
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var agent = await _db.Agents.FirstOrDefaultAsync(a => a.id == agentId && a.team_id == teamId && !a.is_deleted);
        if (agent == null)
        {
            throw new ArgumentException("Agent not found");
        }

        // Only owner or team admin may update the agent
        var userTeamRole = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == teamId && tm.status == "ACTIVE");
        var isAdmin = userTeamRole != null && userTeamRole.team_role_id == 1;
        if (agent.owner_user_id != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only the agent owner or a team admin may update the agent");
        }

        // Ensure name uniqueness within team (excluding this agent)
        if (!string.Equals(agent.name, request.name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _db.Agents.AnyAsync(a => a.team_id == teamId && a.name == request.name && a.id != agentId && !a.is_deleted);
            if (exists) throw new ArgumentException("Agent with the same name already exists");
        }

        // Update fields
        agent.name = request.name;
        agent.description = request.description;
        agent.visibility = request.visibility;
        agent.endpoint_url = request.endpoint_url;
        agent.http_method = request.http_method;
        agent.auth_type = request.auth_type;
        agent.username = request.username;
        // Only update secret fields if non-empty in request (empty = keep existing)
        if (!string.IsNullOrEmpty(request.password)) agent.password = _encryptionService.Encrypt(request.password);
        if (!string.IsNullOrEmpty(request.api_token)) agent.api_token = _encryptionService.Encrypt(request.api_token);
        agent.login_endpoint_url = request.login_endpoint_url;
        agent.request_body_template = request.request_body_template;
        agent.agent_instructions = request.agent_instructions;
        agent.response_mapping = request.response_mapping;
        agent.version = request.version;
        agent.llm_model_id = request.llm_model_id;
        if (!string.IsNullOrEmpty(request.llm_api_key)) agent.encrypted_llm_api_key = _encryptionService.Encrypt(request.llm_api_key);
        agent.updated_at = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return new AvailableAgentResponse
        {
            id = agent.id,
            name = agent.name,
            visibility = agent.visibility,
            endpoint_url = agent.endpoint_url,
            http_method = agent.http_method ?? "GET",
            description = agent.description
        };
    }

    public async Task<IEnumerable<AgentRunResponse>> RunAllAgentsForPageAsync(int userId, int researchPageId)
    {
        var page = await _db.ResearchPages.FirstOrDefaultAsync(p => p.id == researchPageId && !p.is_deleted);
        if (page == null) throw new ArgumentException("Research page not found");

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == page.team_id && tm.status == "ACTIVE");
        if (membership == null) throw new UnauthorizedAccessException("User is not an active member of the team");

        var runs = new List<AgentRun>();

        // Enqueue page-level agents
        var pageAgents = await _db.ResearchPageAgents
            .Where(rpa => rpa.research_page_id == researchPageId)
            .ToListAsync();

        foreach (var rpa in pageAgents)
        {
            runs.Add(new AgentRun
            {
                research_page_agent_id = rpa.id,
                section_agent_id = null,
                section_id = null,
                status = "Queued",
                started_at = null,
                completed_at = null,
                output = null,
                error = null
            });
        }

        // Enqueue section-level agents
        var sectionAgents = await _db.SectionAgents
            .Include(sa => sa.section)
            .Where(sa => sa.section.research_page_id == researchPageId)
            .ToListAsync();

        foreach (var sa in sectionAgents)
        {
            runs.Add(new AgentRun
            {
                research_page_agent_id = null,
                section_agent_id = sa.id,
                section_id = sa.section_id,
                status = "Queued",
                started_at = null,
                completed_at = null,
                output = null,
                error = null
            });
        }

        _db.AgentRuns.AddRange(runs);
        await _db.SaveChangesAsync();

        return runs.Select(r => new AgentRunResponse
        {
            id = r.id,
            research_page_agent_id = r.research_page_agent_id,
            section_agent_id = r.section_agent_id,
            section_id = r.section_id,
            status = r.status,
            started_at = r.started_at,
            completed_at = r.completed_at,
            output = r.output,
            error = r.error
        }).ToList();
    }


}

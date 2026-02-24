using IRS.Application.DTOs.Research;
using IRS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using IRS.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace IRS.Application.Services;

public class ResearchService : IResearchService
{
    private readonly IrsDbContext _db;
    private readonly IStructureAgent _structureAgent;
    private readonly ILogger<ResearchService> _logger;

    public ResearchService(IrsDbContext db, IStructureAgent structureAgent, ILogger<ResearchService> logger)
    {
        _db = db;
        _structureAgent = structureAgent;
        _logger = logger;
    }

    public async Task<ResearchPageResponse> CreateResearchPageAsync(int userId, CreateResearchPageRequest request)
    {
        // Validate membership
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == request.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        // Upsert security
        var security = await _db.Securities.FindAsync(request.figi);
        if (security == null)
        {
            security = new Security
            {
                figi = request.figi,
                ticker = request.ticker,
                name = request.name,
                market_sector = null,
                security_type = request.security_type,
                composite_figi = null,
                exchange_code = null,
                share_class_figi = null,
                mic_code = null,
                security_description = null,
                last_synced_at = DateTime.UtcNow
            };
            _db.Securities.Add(security);
        }
        else
        {
            // Update minimal fields if missing
            security.ticker ??= request.ticker;
            security.name ??= request.name;
            security.security_type ??= request.security_type;
        }

        // Ensure no existing page for team + figi
        var existing = await _db.ResearchPages.FirstOrDefaultAsync(rp => rp.team_id == request.team_id && rp.security_figi == request.figi && !rp.is_deleted);
        if (existing != null)
        {
            throw new ArgumentException("Research page already exists for this team and security");
        }

        var page = new ResearchPage
        {
            team_id = request.team_id,
            security_figi = request.figi,
            conviction_score = null,
            fundamental_score = null,
            created_at = DateTime.UtcNow,
            last_updated = DateTime.UtcNow,
            is_deleted = false
        };
        _db.ResearchPages.Add(page);
        await _db.SaveChangesAsync();

        // Create default sections via StructureAgent
        var titles = await _structureAgent.GetDefaultSectionsAsync(request.security_type);
        foreach (var title in titles)
        {
            _db.Sections.Add(new Section
            {
                research_page_id = page.id,
                title = title,
                fundamental_score = null,
                conviction_score = null,
                section_summary = null,
                ai_generated_content = null,
                is_deleted = false
            });
        }
        await _db.SaveChangesAsync();

        // Build response
        return await BuildResearchPageResponse(page.id);
    }

    public async Task<ResearchPageResponse> GetResearchPageAsync(int userId, int researchPageId)
    {
        var page = await _db.ResearchPages.Include(r => r.team)
            .FirstOrDefaultAsync(r => r.id == researchPageId && !r.is_deleted);
        if (page == null)
        {
            throw new ArgumentException("Research page not found");
        }

        // Validate membership
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        return await BuildResearchPageResponse(researchPageId);
    }

    public async Task<CommentResponse> AddCommentAsync(int userId, int sectionId, CreateCommentRequest request)
    {
        var section = await _db.Sections.Include(s => s.research_page)
            .FirstOrDefaultAsync(s => s.id == sectionId && !s.is_deleted);
        if (section == null)
        {
            throw new ArgumentException("Section not found");
        }

        // Validate membership for the page's team
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var comment = new Comment
        {
            section_id = sectionId,
            author_id = userId,
            author_type = "User",
            author_agent_name = null,
            content = request.content,
            created_at = DateTime.UtcNow,
            is_deleted = false
        };

        _db.Comments.Add(comment);
        await _db.SaveChangesAsync();

        return new CommentResponse
        {
            id = comment.id,
            section_id = comment.section_id,
            author_id = comment.author_id,
            author_type = comment.author_type,
            author_agent_name = comment.author_agent_name,
            content = comment.content,
            created_at = comment.created_at
        };
    }

    public async Task<IEnumerable<CommentResponse>> GetCommentsAsync(int userId, int sectionId)
    {
        var section = await _db.Sections.Include(s => s.research_page)
            .FirstOrDefaultAsync(s => s.id == sectionId && !s.is_deleted);
        if (section == null)
        {
            throw new ArgumentException("Section not found");
        }

        // Validate membership for the page's team
        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not an active member of the team");
        }

        var comments = await _db.Comments
            .Where(c => c.section_id == sectionId && !c.is_deleted)
            .OrderBy(c => c.created_at)
            .Select(c => new CommentResponse
            {
                id = c.id,
                section_id = c.section_id,
                author_id = c.author_id,
                author_type = c.author_type,
                author_agent_name = c.author_agent_name,
                content = c.content,
                created_at = c.created_at
            })
            .ToListAsync();

        return comments;
    }

    public async Task<IEnumerable<ResearchPageListItemResponse>> GetMyResearchPagesAsync(int userId)
    {
        // Get active team memberships
        var teamIds = await _db.TeamMembers
            .Where(tm => tm.user_id == userId && tm.status == "ACTIVE" && !tm.is_deleted)
            .Select(tm => tm.team_id)
            .ToListAsync();

        if (teamIds.Count == 0)
        {
            return Enumerable.Empty<ResearchPageListItemResponse>();
        }

        var pages = await _db.ResearchPages
            .Where(rp => teamIds.Contains(rp.team_id) && !rp.is_deleted)
            .Include(rp => rp.security_figiNavigation)
            .OrderByDescending(rp => rp.last_updated)
            .ThenByDescending(rp => rp.id)
            .Select(rp => new ResearchPageListItemResponse
            {
                id = rp.id,
                team_id = rp.team_id,
                security_figi = rp.security_figi,
                security_ticker = rp.security_figiNavigation.ticker,
                security_name = rp.security_figiNavigation.name,
                security_type = rp.security_figiNavigation.security_type,
                conviction_score = rp.conviction_score,
                fundamental_score = rp.fundamental_score,
                last_updated = rp.last_updated
            })
            .ToListAsync();

        return pages;
    }

    public async Task DeleteResearchPageAsync(int userId, int researchPageId)
    {
        var page = await _db.ResearchPages.Include(p => p.Sections).FirstOrDefaultAsync(p => p.id == researchPageId && !p.is_deleted);
        if (page == null)
            throw new ArgumentException("Research page not found");

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == page.team_id && tm.status == "ACTIVE");
        if (membership == null)
            throw new UnauthorizedAccessException("User is not an active member of the team");

        // Delete agent runs for page-agent attachments
        var pageAgentIds = await _db.ResearchPageAgents.Where(rpa => rpa.research_page_id == researchPageId).Select(rpa => rpa.id).ToListAsync();
        if (pageAgentIds.Any())
        {
            var pageRuns = _db.AgentRuns.Where(ar => ar.research_page_agent_id != null && pageAgentIds.Contains(ar.research_page_agent_id.Value));
            _db.AgentRuns.RemoveRange(pageRuns);
        }

        // Delete section-agent runs and section agents for sections on this page
        var sectionIds = page.Sections.Select(s => s.id).ToList();
        if (sectionIds.Any())
        {
            var sectionAgentIds = await _db.SectionAgents.Where(sa => sectionIds.Contains(sa.section_id)).Select(sa => sa.id).ToListAsync();
            if (sectionAgentIds.Any())
            {
                var sectionRuns = _db.AgentRuns.Where(ar => ar.section_agent_id != null && sectionAgentIds.Contains(ar.section_agent_id.Value));
                _db.AgentRuns.RemoveRange(sectionRuns);
                var sectionAgents = _db.SectionAgents.Where(sa => sectionAgentIds.Contains(sa.id));
                _db.SectionAgents.RemoveRange(sectionAgents);
            }

            // Delete comments for these sections
            var comments = _db.Comments.Where(c => sectionIds.Contains(c.section_id));
            _db.Comments.RemoveRange(comments);

            // Delete sections themselves
            var sections = _db.Sections.Where(s => sectionIds.Contains(s.id));
            _db.Sections.RemoveRange(sections);
        }

        // Delete page-agent attachments
        var pageAgents = _db.ResearchPageAgents.Where(rpa => rpa.research_page_id == researchPageId);
        _db.ResearchPageAgents.RemoveRange(pageAgents);

        // Finally delete any remaining runs linked to the page and the page itself
        var remainingRuns = _db.AgentRuns.Where(ar => (ar.section_id != null && sectionIds.Contains(ar.section_id.Value)) || (ar.research_page_agent_id != null && pageAgentIds.Contains(ar.research_page_agent_id.Value)));
        _db.AgentRuns.RemoveRange(remainingRuns);

        _db.ResearchPages.Remove(page);
        await _db.SaveChangesAsync();
    }

    private async Task<ResearchPageResponse> BuildResearchPageResponse(int pageId)
    {
        var page = await _db.ResearchPages
            .Include(r => r.security_figiNavigation)
            .Include(r => r.Sections)
            .FirstAsync(r => r.id == pageId);

        // Compute aggregated scores as average of non-null section scores
        var secFund = page.Sections.Where(s => s.fundamental_score.HasValue).Select(s => (double)s.fundamental_score!.Value).ToList();
        var secConv = page.Sections.Where(s => s.conviction_score.HasValue).Select(s => (double)s.conviction_score!.Value).ToList();

        int? avgFund = secFund.Count > 0 ? (int?)Math.Round(secFund.Average()) : null;
        int? avgConv = secConv.Count > 0 ? (int?)Math.Round(secConv.Average()) : null;

        // Update page cached scores if different
        bool changed = false;
        if (page.fundamental_score != avgFund) { page.fundamental_score = avgFund; changed = true; }
        if (page.conviction_score != avgConv) { page.conviction_score = avgConv; changed = true; }
        if (changed)
        {
            page.last_updated = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return new ResearchPageResponse
        {
            id = page.id,
            team_id = page.team_id,
            security_figi = page.security_figi,
            security_ticker = page.security_figiNavigation.ticker,
            security_name = page.security_figiNavigation.name,
            security_type = page.security_figiNavigation.security_type,
            conviction_score = page.conviction_score,
            fundamental_score = page.fundamental_score,
            last_updated = page.last_updated,
            sections = page.Sections
                .Where(s => !s.is_deleted)
                .OrderBy(s => s.id)
                .Select(s => new SectionResponse
                {
                    id = s.id,
                    title = s.title,
                    fundamental_score = s.fundamental_score,
                    conviction_score = s.conviction_score,
                    section_summary = s.section_summary,
                    ai_generated_content = s.ai_generated_content
                })
                .ToList()
        };
    }

    public async Task RecalculateAllScoresAsync(int userId)
    {
        // Get active team memberships
        var teamIds = await _db.TeamMembers
            .Where(tm => tm.user_id == userId && tm.status == "ACTIVE" && !tm.is_deleted)
            .Select(tm => tm.team_id)
            .ToListAsync();

        if (teamIds.Count == 0)
        {
            return;
        }

        // Get all active research pages for these teams
        var pages = await _db.ResearchPages
            .Where(rp => teamIds.Contains(rp.team_id) && !rp.is_deleted)
            .Include(rp => rp.Sections.Where(s => !s.is_deleted))
            .ThenInclude(s => s.SectionAgents)
            .ToListAsync();

        var regex = new Regex(@"\{FundamentalScore:\s*(-?\d+),\s*ConvictionScore:\s*(\d+)\}");

        foreach (var page in pages)
        {
            foreach (var section in page.Sections)
            {
                var sectionScores = new List<(int fundamental, int conviction)>();

                // For each section agent, get the latest successful run
                foreach (var sectionAgent in section.SectionAgents)
                {
                    var latestRun = await _db.AgentRuns
                        .Where(ar => ar.section_agent_id == sectionAgent.id 
                                    && ar.status == "Succeeded" 
                                    && ar.output != null)
                        .OrderByDescending(ar => ar.completed_at)
                        .FirstOrDefaultAsync();

                    if (latestRun != null && !string.IsNullOrEmpty(latestRun.output))
                    {
                        var match = regex.Match(latestRun.output);
                        if (match.Success)
                        {
                            if (int.TryParse(match.Groups[1].Value, out int fundamental) &&
                                int.TryParse(match.Groups[2].Value, out int conviction))
                            {
                                sectionScores.Add((fundamental, conviction));
                            }
                        }
                    }
                }

                // Update section scores with averages (excluding nulls)
                if (sectionScores.Count > 0)
                {
                    section.fundamental_score = (int)Math.Round(sectionScores.Average(s => s.fundamental));
                    section.conviction_score = (int)Math.Round(sectionScores.Average(s => s.conviction));
                }
                else
                {
                    section.fundamental_score = null;
                    section.conviction_score = null;
                }
            }

            // Update page scores as average of section scores (excluding nulls)
            var sectionFundamentals = page.Sections
                .Where(s => s.fundamental_score.HasValue)
                .Select(s => (double)s.fundamental_score!.Value)
                .ToList();

            var sectionConvictions = page.Sections
                .Where(s => s.conviction_score.HasValue)
                .Select(s => (double)s.conviction_score!.Value)
                .ToList();

            page.fundamental_score = sectionFundamentals.Count > 0 
                ? (int?)Math.Round(sectionFundamentals.Average()) 
                : null;

            page.conviction_score = sectionConvictions.Count > 0 
                ? (int?)Math.Round(sectionConvictions.Average()) 
                : null;

            page.last_updated = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Recalculated scores for {PageCount} research pages for user {UserId}", pages.Count, userId);
    }

    public async Task<SectionResponse> CreateSectionAsync(int userId, int researchPageId, CreateSectionRequest request)
    {
        var page = await _db.ResearchPages.FirstOrDefaultAsync(p => p.id == researchPageId && !p.is_deleted);
        if (page == null)
            throw new ArgumentException("Research page not found");

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == page.team_id && tm.status == "ACTIVE");
        if (membership == null)
            throw new UnauthorizedAccessException("User is not an active member of the team");

        var section = new Section
        {
            research_page_id = researchPageId,
            title = request.title,
            fundamental_score = null,
            conviction_score = null,
            section_summary = null,
            ai_generated_content = null,
            is_deleted = false
        };

        _db.Sections.Add(section);
        await _db.SaveChangesAsync();

        return new SectionResponse
        {
            id = section.id,
            title = section.title,
            fundamental_score = section.fundamental_score,
            conviction_score = section.conviction_score,
            section_summary = section.section_summary,
            ai_generated_content = section.ai_generated_content
        };
    }

    public async Task DeleteSectionAsync(int userId, int sectionId)
    {
        var section = await _db.Sections.Include(s => s.research_page)
            .FirstOrDefaultAsync(s => s.id == sectionId && !s.is_deleted);
        if (section == null)
            throw new ArgumentException("Section not found");

        var membership = await _db.TeamMembers.FirstOrDefaultAsync(tm => tm.user_id == userId && tm.team_id == section.research_page.team_id && tm.status == "ACTIVE");
        if (membership == null)
            throw new UnauthorizedAccessException("User is not an active member of the team");

        // Soft delete the section
        section.is_deleted = true;
        section.deleted_at = DateTime.UtcNow;

        // Also soft delete associated comments and agent runs
        var comments = _db.Comments.Where(c => c.section_id == sectionId);
        foreach (var comment in comments)
        {
            comment.is_deleted = true;
            comment.deleted_at = DateTime.UtcNow;
        }

        // Delete section agents and their runs
        var sectionAgents = await _db.SectionAgents.Where(sa => sa.section_id == sectionId).ToListAsync();
        foreach (var sa in sectionAgents)
        {
            var runs = _db.AgentRuns.Where(ar => ar.section_agent_id == sa.id);
            _db.AgentRuns.RemoveRange(runs);
        }
        _db.SectionAgents.RemoveRange(sectionAgents);

        await _db.SaveChangesAsync();
    }
}

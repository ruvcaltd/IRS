using IRS.Application.DTOs.Research;

namespace IRS.Application.Services;

public interface IResearchService
{
    Task<ResearchPageResponse> CreateResearchPageAsync(int userId, CreateResearchPageRequest request);
    Task<ResearchPageResponse> GetResearchPageAsync(int userId, int researchPageId);
    Task<CommentResponse> AddCommentAsync(int userId, int sectionId, CreateCommentRequest request);
    Task<IEnumerable<CommentResponse>> GetCommentsAsync(int userId, int sectionId);
    Task<IEnumerable<ResearchPageListItemResponse>> GetMyResearchPagesAsync(int userId);

    // Delete a research page and all associated data (agents, runs, sections, comments)
    Task DeleteResearchPageAsync(int userId, int researchPageId);

    // Recalculate fundamental and conviction scores for all active research pages accessible to the user
    Task RecalculateAllScoresAsync(int userId);

    // Section management
    Task<SectionResponse> CreateSectionAsync(int userId, int researchPageId, CreateSectionRequest request);
    Task DeleteSectionAsync(int userId, int sectionId);
}

import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { BaseApiService } from '../core/base-api.service';
import {
  AgentRunResponse,
  AttachAgentRequest,
  AvailableAgentResponse,
  CommentResponse,
  CreateCommentRequest,
  CreateResearchPageRequest,
  CreateSectionRequest,
  PageAgentResponse,
  ResearchPageResponse,
  ResearchPageListItemResponse,
  SectionResponse,
  SecuritySearchItem,
  ToggleAgentRequest
} from '../../generated/generated/irs-api.client';

@Injectable({ providedIn: 'root' })
export class ResearchService extends BaseApiService {
  searchSecurities(query: string): Observable<SecuritySearchItem[]> {
    return this.apiClient.securities_Search(query).pipe(map(r => r.result ?? []));
  }

  createResearchPage(teamId: number, security: SecuritySearchItem): Observable<ResearchPageResponse> {
    const request = new CreateResearchPageRequest({
      team_id: teamId,
      figi: security.figi ?? '',
      ticker: security.ticker,
      name: security.name,
      security_type: security.security_type ?? 'Corporate'
    });

    return this.apiClient.researchPages_Create(request).pipe(map(r => r.result));
  }

  getResearchPage(pageId: number): Observable<ResearchPageResponse> {
    return this.apiClient.researchPages_GetById(pageId).pipe(map(r => r.result));
  }

  getComments(sectionId: number): Observable<CommentResponse[]> {
    return this.apiClient.sections_GetComments(sectionId).pipe(map(r => r.result ?? []));
  }

  addComment(sectionId: number, content: string): Observable<CommentResponse> {
    const request = new CreateCommentRequest({ content });
    return this.apiClient.sections_AddComment(sectionId, request).pipe(map(r => r.result));
  }

  getAvailableAgents(teamId: number): Observable<AvailableAgentResponse[]> {
    return this.apiClient.agents_GetAvailableAgents(teamId).pipe(map(r => r.result ?? []));
  }

  getPageAgents(pageId: number): Observable<PageAgentResponse[]> {
    return this.apiClient.agents_GetPageAgents(pageId).pipe(map(r => r.result ?? []));
  }

  attachAgent(pageId: number, agentId: number): Observable<PageAgentResponse> {
    const request = new AttachAgentRequest({ agent_id: agentId });
    return this.apiClient.agents_AttachAgent(pageId, request).pipe(map(r => r.result));
  }

  toggleAgent(pageAgentId: number, isEnabled: boolean): Observable<PageAgentResponse> {
    const request = new ToggleAgentRequest({ is_enabled: isEnabled });
    return this.apiClient.agents_ToggleEnabled(pageAgentId, request).pipe(map(r => r.result));
  }

  getAgentRuns(pageAgentId: number): Observable<AgentRunResponse[]> {
    return this.apiClient.agents_GetRuns(pageAgentId).pipe(map(r => r.result ?? []));
  }

  /** Fetch only the most recent run for a page-agent (server-side) */
  getLatestAgentRun(pageAgentId: number): Observable<AgentRunResponse | null> {
    return this.apiClient.agents_GetLatestRun(pageAgentId).pipe(map(r => r.result ?? null));
  }

  runAgent(pageAgentId: number): Observable<AgentRunResponse> {
    return this.apiClient.agents_EnqueueRun(pageAgentId).pipe(map(r => r.result!));
  }

  getSectionAgents(sectionId: number): Observable<PageAgentResponse[]> {
    return this.apiClient.agents_GetSectionAgents(sectionId).pipe(map(r => r.result ?? []));
  }

  attachSectionAgent(sectionId: number, agentId: number): Observable<PageAgentResponse> {
    const request = new AttachAgentRequest({ agent_id: agentId });
    return this.apiClient.agents_AttachSectionAgent(sectionId, request).pipe(map(r => r.result));
  }

  toggleSectionAgent(sectionAgentId: number, isEnabled: boolean): Observable<PageAgentResponse> {
    const request = new ToggleAgentRequest({ is_enabled: isEnabled });
    return this.apiClient.agents_ToggleSectionEnabled(sectionAgentId, request).pipe(map(r => r.result));
  }

  runSectionAgent(sectionAgentId: number): Observable<AgentRunResponse> {
    return this.apiClient.agents_EnqueueSectionRun(sectionAgentId).pipe(map(r => r.result!));
  }

  getLatestSectionAgentRun(sectionAgentId: number): Observable<AgentRunResponse | null> {
    return this.apiClient.agents_GetSectionRuns(sectionAgentId).pipe(
      map(r => {
        const runs = r.result ?? [];
        // Sort by started_at descending and take the first (latest)
        return runs.sort((a, b) => new Date(b.started_at!).getTime() - new Date(a.started_at!).getTime())[0] ?? null;
      })
    );
  }

  /** Delete a page-level agent association and all past runs */
  deletePageAgent(pageAgentId: number): Observable<void> {
    return this.apiClient.agents_DeletePageAgent(pageAgentId).pipe(map(() => undefined));
  }

  /** Delete a section-level agent association and all past runs */
  deleteSectionAgent(sectionAgentId: number): Observable<void> {
    return this.apiClient.agents_DeleteSectionAgent(sectionAgentId).pipe(map(() => undefined));
  }

  /** Delete a research page and all associated agents/runs/sections/comments */
  deleteResearchPage(researchPageId: number): Observable<void> {
    return this.apiClient.researchPages_Delete(researchPageId).pipe(map(() => undefined));
  }

  /** Enqueue ALL page + section agents attached to a research page in one API call */
  runAllAgents(pageId: number): Observable<AgentRunResponse[]> {
    return this.apiClient.researchPages_RunAll(pageId).pipe(map(r => r.result ?? []));
  }

  // List research pages visible to current user
  getMyResearch(): Observable<ResearchPageListItemResponse[]> {
    return this.apiClient.researchPages_GetMy().pipe(map(r => r.result ?? []));
  }

  /** Recalculate scores for all active research pages */
  recalculateScores(): Observable<void> {
    return this.apiClient.researchPages_RecalculateScores().pipe(map(() => undefined));
  }

  /** Create a new section in a research page */
  createSection(researchPageId: number, title: string): Observable<SectionResponse> {
    const request = new CreateSectionRequest({ title });
    return this.apiClient.sections_CreateSection(researchPageId, request).pipe(map(r => r.result));
  }

  /** Delete a section from a research page */
  deleteSection(sectionId: number): Observable<void> {
    return this.apiClient.sections_DeleteSection(sectionId).pipe(map(() => undefined));
  }
}


import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { BaseApiService } from '../core/base-api.service';
import { AvailableAgentResponse, CreateAgentRequest, AgentValidationResponse, AgentDetailResponse, SectionAgentResponse, AttachAgentRequest, ToggleAgentRequest, AgentRunResponse } from '../../generated/generated/irs-api.client';

@Injectable({ providedIn: 'root' })
export class AgentsService extends BaseApiService {
  createAgent(teamId: number, payload: CreateAgentRequest): Observable<AvailableAgentResponse | undefined> {
    return this.apiClient.agents_CreateAgent(teamId, payload).pipe(map(r => r.result));
  }

  // Reuse existing helpers
  getAvailableAgents(teamId: number): Observable<AvailableAgentResponse[]> {
    return this.apiClient.agents_GetAvailableAgents(teamId).pipe(map(r => r.result ?? []));
  }

  getAgent(teamId: number, agentId: number): Observable<AgentDetailResponse | undefined> {
    return this.apiClient.agents_GetAgent(teamId, agentId).pipe(map(r => r.result));
  }

  updateAgent(teamId: number, agentId: number, payload: CreateAgentRequest): Observable<AvailableAgentResponse | undefined> {
    return this.apiClient.agents_UpdateAgent(teamId, agentId, payload).pipe(map(r => r.result));
  }

  validateAgent(teamId: number, payload: CreateAgentRequest): Observable<AgentValidationResponse | undefined> {
    return this.apiClient.agents_ValidateAgent(teamId, payload).pipe(map(r => r.result));
  }

  // Section agent methods
  attachSectionAgent(sectionId: number, agentId: number): Observable<SectionAgentResponse | undefined> {
    const request = new AttachAgentRequest({ agent_id: agentId });
    return this.apiClient.agents_AttachSectionAgent(sectionId, request).pipe(map(r => r.result));
  }

  getSectionAgents(sectionId: number): Observable<SectionAgentResponse[]> {
    return this.apiClient.agents_GetSectionAgents(sectionId).pipe(map(r => r.result ?? []));
  }

  setSectionAgentEnabled(sectionAgentId: number, isEnabled: boolean): Observable<SectionAgentResponse | undefined> {
    const request = new ToggleAgentRequest({ is_enabled: isEnabled });
    return this.apiClient.agents_ToggleSectionEnabled(sectionAgentId, request).pipe(map(r => r.result));
  }

  enqueueSectionRun(sectionAgentId: number): Observable<AgentRunResponse | undefined> {
    return this.apiClient.agents_EnqueueSectionRun(sectionAgentId).pipe(map(r => r.result));
  }

  getSectionAgentRuns(sectionAgentId: number): Observable<AgentRunResponse[]> {
    return this.apiClient.agents_GetSectionRuns(sectionAgentId).pipe(map(r => r.result ?? []));
  }
}
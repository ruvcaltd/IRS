import { Injectable } from '@angular/core';
import { BaseApiService } from '../core/base-api.service';
import { Observable, map } from 'rxjs';
import {
  TeamResponse,
  CreateTeamRequest,
  JoinTeamRequest,
  TeamMemberResponse,
  ApproveMemberRequest,
  SwaggerResponse
} from '../../generated/generated/irs-api.client';

@Injectable({ providedIn: 'root' })
export class TeamsService extends BaseApiService {
  getMyTeams(): Observable<TeamResponse[]> {
    return this.apiClient.teams_GetUserTeams().pipe(map((r: SwaggerResponse<TeamResponse[]>) => r.result ?? []));
  }

  createTeam(name: string): Observable<TeamResponse> {
    const req = new CreateTeamRequest({ name });
    return this.apiClient.teams_CreateTeam(req).pipe(map(r => r.result));
  }

  searchTeams(query: string): Observable<TeamResponse[]> {
    return this.apiClient.teams_SearchTeams(query).pipe(map(r => r.result ?? []));
  }

  requestJoin(teamId: number): Observable<void> {
    const req = new JoinTeamRequest({ team_id: teamId });
    return this.apiClient.teams_JoinTeam(req).pipe(map(() => void 0));
  }

  getTeamMembers(teamId: number): Observable<TeamMemberResponse[]> {
    return this.apiClient.teams_GetTeamMembers(teamId).pipe(map(r => r.result ?? []));
  }

  getPendingRequests(teamId: number): Observable<TeamMemberResponse[]> {
    return this.apiClient.teams_GetPendingRequests(teamId).pipe(map(r => r.result ?? []));
  }

  approveMember(teamId: number, userId: number, roleId: number): Observable<TeamMemberResponse> {
    const req = new ApproveMemberRequest({ user_id: userId, team_role_id: roleId });
    return this.apiClient.teams_ApproveMember(teamId, userId, req).pipe(map(r => r.result));
  }

  rejectMember(teamId: number, userId: number): Observable<void> {
    return this.apiClient.teams_RejectMember(teamId, userId).pipe(map(() => void 0));
  }
}

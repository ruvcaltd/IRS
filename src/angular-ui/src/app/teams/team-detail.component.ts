import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TeamsService } from './teams.service';
import { TeamMemberResponse, TeamResponse } from '../../generated/generated/irs-api.client';


@Component({
  selector: 'app-team-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './team-detail.component.html',
  styleUrl: './team-detail.component.scss'
})
export class TeamDetailComponent {
  constructor(
    private readonly route: ActivatedRoute,
    private readonly teamsService: TeamsService
  ) {
    this.teamId = Number(this.route.snapshot.paramMap.get('id'));
    this.loadMyRole();
    this.loadMembers();
  }

  teamId: number;

  state = signal<{
    role: string | null;
    members: TeamMemberResponse[];
    pending: TeamMemberResponse[];
    isLoadingMembers: boolean;
    isLoadingPending: boolean;
    error: string | null;
  }>({
    role: null,
    members: [],
    pending: [],
    isLoadingMembers: false,
    isLoadingPending: false,
    error: null
  });

  role = computed(() => this.state().role);
  members = computed(() => this.state().members);
  pending = computed(() => this.state().pending);
  isAdmin = computed(() => (this.state().role ?? '').toLowerCase() === 'admin');
  isLoadingMembers = computed(() => this.state().isLoadingMembers);
  isLoadingPending = computed(() => this.state().isLoadingPending);
  error = computed(() => this.state().error);

  private loadMyRole(): void {
    this.teamsService.getMyTeams().subscribe({
      next: (teams: TeamResponse[]) => {
        const current = teams.find(t => t.id === this.teamId);
        this.state.update(s => ({ ...s, role: current?.current_user_role ?? null }));
        if ((current?.current_user_role ?? '').toLowerCase() === 'admin') {
          this.loadPending();
        }
      },
      error: () => this.state.update(s => ({ ...s, role: null }))
    });
  }

  private loadMembers(): void {
    this.state.update(s => ({ ...s, isLoadingMembers: true, error: null }));
    this.teamsService.getTeamMembers(this.teamId).subscribe({
      next: members => this.state.update(s => ({ ...s, members, isLoadingMembers: false })),
      error: err => {
        const message = err?.result?.detail || err?.message || 'Failed to load team members.';
        this.state.update(s => ({ ...s, members: [], isLoadingMembers: false, error: message }));
      }
    });
  }

  private loadPending(): void {
    this.state.update(s => ({ ...s, isLoadingPending: true }));
    this.teamsService.getPendingRequests(this.teamId).subscribe({
      next: pending => this.state.update(s => ({ ...s, pending, isLoadingPending: false })),
      error: () => this.state.update(s => ({ ...s, pending: [], isLoadingPending: false }))
    });
  }

  approve(userId: number, roleId: number): void {
    this.teamsService.approveMember(this.teamId, userId, roleId).subscribe({
      next: () => {
        // refresh members and pending lists
        this.loadMembers();
        this.loadPending();
      }
    });
  }

  reject(userId: number): void {
    this.teamsService.rejectMember(this.teamId, userId).subscribe({
      next: () => this.loadPending()
    });
  }

  trackByUser = (_: number, m: TeamMemberResponse) => m.user_id;
}

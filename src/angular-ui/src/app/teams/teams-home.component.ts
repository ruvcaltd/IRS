import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TeamsService } from './teams.service';
import { TeamResponse } from '../../generated/generated/irs-api.client';

@Component({
  selector: 'app-teams-home',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './teams-home.component.html',
  styleUrl: './teams-home.component.scss'
})
export class TeamsHomeComponent {
  constructor(private readonly teamsService: TeamsService) {
    this.loadTeams();
  }

  state = signal<{ teams: TeamResponse[]; isLoading: boolean; error: string | null }>({
    teams: [],
    isLoading: false,
    error: null
  });

  teams = computed(() => this.state().teams);
  isLoading = computed(() => this.state().isLoading);
  error = computed(() => this.state().error);

  private loadTeams(): void {
    this.state.update(s => ({ ...s, isLoading: true, error: null }));
    this.teamsService.getMyTeams().subscribe({
      next: teams => this.state.set({ teams, isLoading: false, error: null }),
      error: err => {
        const message = err?.result?.detail || err?.message || 'Failed to load teams.';
        this.state.set({ teams: [], isLoading: false, error: message });
      }
    });
  }

  trackByTeam = (_: number, t: TeamResponse) => t.id;
}

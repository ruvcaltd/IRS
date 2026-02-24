import { Component, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TeamsService } from './teams.service';
import { TeamResponse } from '../../generated/generated/irs-api.client';

@Component({
  selector: 'app-team-search',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './team-search.component.html',
  styleUrl: './team-search.component.scss'
})
export class TeamSearchComponent {
  constructor(private readonly teamsService: TeamsService) {}

  query = '';

  state = signal<{ results: TeamResponse[]; isLoading: boolean; error: string | null; notice: string | null }>(
    { results: [], isLoading: false, error: null, notice: null }
  );

  results = computed(() => this.state().results);
  isLoading = computed(() => this.state().isLoading);
  error = computed(() => this.state().error);
  notice = computed(() => this.state().notice);

  onSearch(): void {
    const q = this.query.trim();
    if (!q) return;
    this.state.set({ results: [], isLoading: true, error: null, notice: null });
    this.teamsService.searchTeams(q).subscribe({
      next: results => this.state.set({ results, isLoading: false, error: null, notice: null }),
      error: err => {
        const message = err?.result?.detail || err?.message || 'Search failed.';
        this.state.set({ results: [], isLoading: false, error: message, notice: null });
      }
    });
  }

  requestJoin(teamId: number): void {
    this.teamsService.requestJoin(teamId).subscribe({
      next: () => this.state.update(s => ({ ...s, notice: 'Join request submitted for approval.' })),
      error: err => {
        const message = err?.result?.detail || err?.message || 'Failed to submit join request.';
        this.state.update(s => ({ ...s, error: message }));
      }
    });
  }

  trackByTeam = (_: number, t: TeamResponse) => t.id;
}

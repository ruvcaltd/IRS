import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { SecuritySearchItem, TeamResponse } from '../../generated/generated/irs-api.client';
import { TeamsService } from '../teams/teams.service';
import { ResearchService } from './research.service';

@Component({
  selector: 'app-research-workspace',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './research-workspace.component.html',
  styleUrl: './research-workspace.component.scss'
})
export class ResearchWorkspaceComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly teamsService = inject(TeamsService);
  private readonly researchService = inject(ResearchService);

  form: FormGroup = this.fb.group({
    query: ['', [Validators.required, Validators.minLength(2)]],
    teamId: [null as number | null, Validators.required]
  });

  state = signal({
    teams: [] as TeamResponse[],
    isLoadingTeams: false,
    isSearching: false,
    searchResults: [] as SecuritySearchItem[],
    error: null as string | null,
    notice: null as string | null,
    creatingFigi: null as string | null
  });

  teams = computed(() => this.state().teams);
  isLoadingTeams = computed(() => this.state().isLoadingTeams);
  searchResults = computed(() => this.state().searchResults);
  isSearching = computed(() => this.state().isSearching);
  error = computed(() => this.state().error);
  notice = computed(() => this.state().notice);
  creatingFigi = computed(() => this.state().creatingFigi);

  ngOnInit(): void {
    this.loadTeams();
  }

  private loadTeams(): void {
    this.state.update(s => ({ ...s, isLoadingTeams: true, error: null }));
    this.teamsService.getMyTeams().subscribe({
      next: teams => {
        this.state.update(s => ({ ...s, teams, isLoadingTeams: false }));
        if (teams.length > 0 && !this.form.get('teamId')?.value) {
          this.form.get('teamId')?.setValue(teams[0].id ?? null);
        }
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Unable to load your teams right now.';
        this.state.update(s => ({ ...s, teams: [], isLoadingTeams: false, error: message }));
      }
    });
  }

  onSearch(): void {
    if (this.form.get('query')?.invalid) {
      this.form.get('query')?.markAsTouched();
      return;
    }

    const query = (this.form.get('query')?.value as string).trim();
    if (!query) return;

    this.state.update(s => ({ ...s, isSearching: true, error: null, notice: null, searchResults: [] }));

    this.researchService.searchSecurities(query).subscribe({
      next: results => this.state.update(s => ({ ...s, searchResults: results, isSearching: false, notice: results.length === 0 ? 'No securities found. Try a different query.' : null })),
      error: err => {
        const message = err?.result?.detail || err?.message || 'Search failed. Please retry.';
        this.state.update(s => ({ ...s, isSearching: false, error: message, searchResults: [] }));
      }
    });
  }

  createPage(security: SecuritySearchItem): void {
    const rawTeamId = this.form.get('teamId')?.value;
    const teamId = rawTeamId !== null && rawTeamId !== undefined ? Number(rawTeamId) : null;
    if (!teamId) {
      this.state.update(s => ({ ...s, error: 'Select a team before creating a research page.' }));
      return;
    }

    this.state.update(s => ({ ...s, creatingFigi: security.figi ?? null, error: null }));

    this.researchService.createResearchPage(teamId, security).subscribe({
      next: page => {
        this.state.update(s => ({ ...s, creatingFigi: null, notice: 'Structure agent is preparing your sections.' }));
        if (page?.id) {
          this.router.navigate(['/research', page.id]);
        }
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Could not create the research page.';
        this.state.update(s => ({ ...s, creatingFigi: null, error: message }));
      }
    });
  }

  trackBySecurity = (_: number, item: SecuritySearchItem) => `${item.figi}-${item.ticker}`;
  trackByTeam = (_: number, item: TeamResponse) => item.id;
}

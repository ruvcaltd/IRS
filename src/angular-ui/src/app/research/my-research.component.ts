import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ResearchService } from './research.service';
import { ResearchPageListItemResponse } from '../../generated/generated/irs-api.client';
import { ConfirmModalComponent } from '../shared/confirm-modal/confirm-modal.component';
import { ToastService } from '../core/toast.service';
import { ScoreIndicatorComponent } from '../shared/score-indicator/score-indicator.component';

@Component({
  selector: 'app-my-research',
  standalone: true,
  imports: [CommonModule, RouterLink, ScoreIndicatorComponent, ConfirmModalComponent],
  templateUrl: './my-research.component.html',
  styleUrl: './my-research.component.scss'
})
export class MyResearchComponent implements OnInit {
  private readonly researchService = inject(ResearchService);
  private readonly router = inject(Router);
  private readonly toastService = inject(ToastService);

  state = signal({
    items: [] as ResearchPageListItemResponse[],
    searchQuery: '',
    isLoading: true,
    error: null as string | null
  });

  // confirm modal state for deleting research pages
  confirmState = signal<{ item: ResearchPageListItemResponse } | null>(null);

  items = computed(() => {
    const query = this.state().searchQuery.toLowerCase();
    if (!query) return this.state().items;
    return this.state().items.filter(item =>
      item.security_ticker?.toLowerCase().includes(query) ||
      item.security_name?.toLowerCase().includes(query) ||
      item.security_figi?.toLowerCase().includes(query)
    );
  });
  isLoading = computed(() => this.state().isLoading);
  error = computed(() => this.state().error);

  onSearch(query: string): void {
    this.state.update(s => ({ ...s, searchQuery: query }));
  }

  ngOnInit(): void {
    this.researchService.getMyResearch().subscribe({
      next: items => this.state.update(s => ({ ...s, items, isLoading: false })),
      error: err => {
        const message = err?.result?.detail ?? err?.message ?? 'Unable to load your research.';
        this.state.update(s => ({ ...s, error: message, isLoading: false }));
      }
    });
  }

  open(item: ResearchPageListItemResponse): void {
    if (!item.id) return;
    this.router.navigate(['/research', item.id]);
  }

  runAllAgents(item: ResearchPageListItemResponse, event?: MouseEvent): void {
    if (!item.id) return;
    this.state.update(s => ({ ...s, isLoading: true }));
    this.researchService.runAllAgents(item.id).subscribe({
      next: () => this.state.update(s => ({ ...s, isLoading: false })),
      error: (err: any) => this.state.update(s => ({ ...s, isLoading: false, error: err?.result?.detail || err?.message || 'Unable to run agents.' }))
    });
  }

  deleteResearch(item: ResearchPageListItemResponse, event?: MouseEvent): void {
    if (!item.id) return;
    // show modal instead of native confirm
    this.confirmState.set({ item });
  }

  confirmDeleteResearch(): void {
    const ctx = this.confirmState();
    if (!ctx?.item?.id) { this.confirmState.set(null); return; }

    const id = ctx.item.id;
    this.state.update(s => ({ ...s, isLoading: true }));
    this.researchService.deleteResearchPage(id).subscribe({
      next: () => {
        this.state.update(s => ({ ...s, items: s.items.filter(i => i.id !== id), isLoading: false }));
        this.toastService.showSuccess('Research page deleted.');
        this.confirmState.set(null);
      },
      error: (err: any) => {
        const message = err?.result?.detail || err?.message || 'Unable to delete research.';
        this.state.update(s => ({ ...s, isLoading: false, error: message }));
        this.toastService.showError(message);
        this.confirmState.set(null);
      }
    });
  }

  cancelConfirm(): void { this.confirmState.set(null); }

  recalculateScores(): void {
    this.state.update(s => ({ ...s, isLoading: true }));
    this.researchService.recalculateScores().subscribe({
      next: () => {
        // Refresh the list to show updated scores
        this.researchService.getMyResearch().subscribe({
          next: items => {
            this.state.update(s => ({ ...s, items, isLoading: false }));
            this.toastService.showSuccess('Scores recalculated successfully');
          },
          error: err => {
            const message = err?.result?.detail ?? err?.message ?? 'Unable to reload research.';
            this.state.update(s => ({ ...s, error: message, isLoading: false }));
            this.toastService.showError(message);
          }
        });
      },
      error: (err: any) => {
        const message = err?.result?.detail || err?.message || 'Unable to recalculate scores.';
        this.state.update(s => ({ ...s, isLoading: false, error: message }));
        this.toastService.showError(message);
      }
    });
  }

  trackByItem = (_: number, item: ResearchPageListItemResponse) => item.id;
}

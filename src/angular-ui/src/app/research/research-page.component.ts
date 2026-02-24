import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  AgentRunResponse,
  AvailableAgentResponse,
  CommentResponse,
  PageAgentResponse,
  ResearchPageResponse,
  SectionResponse,
  SectionAgentResponse
} from '../../generated/generated/irs-api.client';
import { ResearchService } from './research.service';
import { ScoreIndicatorComponent } from '../shared/score-indicator/score-indicator.component';
import { ConfirmModalComponent } from '../shared/confirm-modal/confirm-modal.component';
import { ToastService } from '../core/toast.service';

@Component({
  selector: 'app-research-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, ScoreIndicatorComponent, ConfirmModalComponent],
  templateUrl: './research-page.component.html',
  styleUrl: './research-page.component.scss'
})
export class ResearchPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly researchService = inject(ResearchService);
  private readonly toastService = inject(ToastService);
  private refreshInterval: any = null;

  pageId = Number(this.route.snapshot.paramMap.get('id'));

  page = signal<ResearchPageResponse | null>(null);
  isLoadingPage = signal(true);
  pageError = signal<string | null>(null);

  commentsBySection = signal<Record<number, CommentResponse[]>>({});
  commentsLoading = signal<Record<number, boolean>>({});
  drafts = signal<Record<number, string>>({});
  expandedComments = signal<Set<number>>(new Set());

  availableAgents = signal<AvailableAgentResponse[]>([]);
  pageAgents = signal<PageAgentResponse[]>([]);
  agentRuns = signal<Record<number, AgentRunResponse | null>>({});
  agentRunsLoading = signal<Record<number, boolean>>({});
  agentsLoading = signal(false);
  attachState = signal<{ selectedAgentId: number | null; isAttaching: boolean; error: string | null }>(
    { selectedAgentId: null, isAttaching: false, error: null }
  );

  // Section agents
  sectionAgents = signal<Record<number, SectionAgentResponse[]>>({});
  sectionAgentRuns = signal<Record<number, AgentRunResponse | null>>({});
  sectionAgentRunsLoading = signal<Record<number, boolean>>({});
  sectionAttachState = signal<Record<number, { selectedAgentId: number | null; isAttaching: boolean; error: string | null }>>({});

  // confirm modal state for delete actions
  confirmState = signal<{ kind: 'page-agent' | 'section-agent' | 'section'; agent?: any; section?: any } | null>(null);

  sections = computed(() => this.page()?.sections ?? []);
  convictionScore = computed(() => this.page()?.conviction_score ?? null);
  fundamentalScore = computed(() => this.page()?.fundamental_score ?? null);
  unattachedAgents = computed(() => {
    const attachedIds = new Set(this.pageAgents().map(a => a.agent_id));
    return this.availableAgents().filter(a => !attachedIds.has(a.id));
  });

  hasQueuedRuns = computed(() => {
    // Check page agent runs
    const pageRuns = this.agentRuns();
    const hasQueuedPageRuns = Object.values(pageRuns).some(run => run?.status === 'Queued' || run?.status === 'Running');

    // Check section agent runs
    const sectionRuns = this.sectionAgentRuns();
    const hasQueuedSectionRuns = Object.values(sectionRuns).some(run => run?.status === 'Queued' || run?.status === 'Running');

    return hasQueuedPageRuns || hasQueuedSectionRuns;
  });

  constructor() {
    // Auto-refresh runs every 10 seconds when there are queued runs
    effect(() => {
      if (this.hasQueuedRuns()) {
        if (!this.refreshInterval) {
          this.refreshInterval = setInterval(() => {
            // Refresh page agent runs
            this.pageAgents().forEach(agent => {
              if (agent.id !== undefined && agent.id !== null) {
                const run = this.agentRuns()[agent.id];
                if (run?.status === 'Queued' || run?.status === 'Running') {
                  this.loadRuns(agent.id);
                }
              }
            });

            // Refresh section agent runs
            Object.entries(this.sectionAgentRuns()).forEach(([sectionAgentId, run]) => {
              if (run?.status === 'Queued' || run?.status === 'Running') {
                this.loadSectionRuns(parseInt(sectionAgentId));
              }
            });
          }, 10000);
        }
      } else {
        if (this.refreshInterval) {
          clearInterval(this.refreshInterval);
          this.refreshInterval = null;
        }
      }
    });
  }

  ngOnInit(): void {
    this.loadPage();
  }

  ngOnDestroy(): void {
    if (this.refreshInterval) {
      clearInterval(this.refreshInterval);
      this.refreshInterval = null;
    }
  }

  private loadPage(): void {
    this.isLoadingPage.set(true);
    this.pageError.set(null);

    this.researchService.getResearchPage(this.pageId).subscribe({
      next: page => {
        this.page.set(page);
        this.isLoadingPage.set(false);
        this.loadAgents();
        this.loadPageAgents();
        (page.sections ?? []).forEach(section => {
          if (section.id !== undefined && section.id !== null) {
            this.loadComments(section.id);
            this.loadSectionAgents(section.id);
          }
        });
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Unable to load the research page.';
        this.pageError.set(message);
        this.isLoadingPage.set(false);
      }
    });
  }

  private loadComments(sectionId: number): void {
    this.commentsLoading.update(state => ({ ...state, [sectionId]: true }));
    this.researchService.getComments(sectionId).subscribe({
      next: comments => {
        this.commentsBySection.update(state => ({ ...state, [sectionId]: comments }));
        this.commentsLoading.update(state => ({ ...state, [sectionId]: false }));
      },
      error: () => this.commentsLoading.update(state => ({ ...state, [sectionId]: false }))
    });
  }

  submitComment(sectionId: number): void {
    const draft = (this.drafts()[sectionId] ?? '').trim();
    if (!draft) return;

    this.commentsLoading.update(state => ({ ...state, [sectionId]: true }));
    this.researchService.addComment(sectionId, draft).subscribe({
      next: comment => {
        const updated = [...(this.commentsBySection()[sectionId] ?? []), comment];
        this.commentsBySection.update(state => ({ ...state, [sectionId]: updated }));
        this.drafts.update(state => ({ ...state, [sectionId]: '' }));
        this.commentsLoading.update(state => ({ ...state, [sectionId]: false }));
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Unable to post comment.';
        this.pageError.set(message);
        this.commentsLoading.update(state => ({ ...state, [sectionId]: false }));
      }
    });
  }

  updateDraft(sectionId: number, value: string): void {
    this.drafts.update(state => ({ ...state, [sectionId]: value }));
  }

  private loadAgents(): void {
    const teamId = this.page()?.team_id;
    if (!teamId) return;
    this.agentsLoading.set(true);
    this.researchService.getAvailableAgents(teamId).subscribe({
      next: agents => {
        this.availableAgents.set(agents);
        this.agentsLoading.set(false);
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Unable to load agents.';
        this.pageError.set(message);
        this.agentsLoading.set(false);
      }
    });
  }

  private loadPageAgents(): void {
    this.researchService.getPageAgents(this.pageId).subscribe({
      next: agents => {
        this.pageAgents.set(agents);
        agents.forEach(agent => {
          if (agent.id !== undefined && agent.id !== null) {
            this.loadRuns(agent.id);
          }
        });
      },
      error: err => this.pageError.set(err?.result?.detail || err?.message || 'Unable to load page agents.')
    });
  }

  attachAgent(): void {
    const selectedAgentId = this.attachState().selectedAgentId;
    if (!selectedAgentId) {
      this.attachState.update(state => ({ ...state, error: 'Choose an agent to attach.' }));
      return;
    }
    this.attachState.update(state => ({ ...state, isAttaching: true, error: null }));

    this.researchService.attachAgent(this.pageId, selectedAgentId).subscribe({
      next: agent => {
        this.pageAgents.update(list => [...list, agent]);
        this.attachState.set({ selectedAgentId: null, isAttaching: false, error: null });
        if (agent.id !== undefined && agent.id !== null) {
          this.loadRuns(agent.id);
        }
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Unable to attach agent.';
        this.attachState.update(state => ({ ...state, isAttaching: false, error: message }));
      }
    });
  }

  toggleAgent(agent: PageAgentResponse): void {
    if (!agent.id) return;
    this.researchService.toggleAgent(agent.id, !agent.is_enabled).subscribe({
      next: updated => {
        this.pageAgents.update(list => list.map(a => (a.id === updated.id ? updated : a)));
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Unable to update agent state.';
        this.pageError.set(message);
      }
    });
  }

  runAgentNow(agent: PageAgentResponse): void {
    if (!agent.id) return;
    this.researchService.runAgent(agent.id).subscribe({
      next: run => {
        // refresh runs for this agent
        this.loadRuns(agent.id!);
        // update page agent quick state
        this.pageAgents.update(list => list.map(a => a.id === agent.id ? Object.assign(a, { last_run_status: run.status, last_run_at: run.completed_at }) : a));
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Unable to run agent.';
        this.pageError.set(message);
      }
    });
  }

  deletePageAgent(agent: PageAgentResponse): void {
    if (!agent.id) return;
    // open confirm modal instead of native confirm
    this.confirmState.set({ kind: 'page-agent', agent });
  }

  deleteSectionAgent(agent: SectionAgentResponse): void {
    if (!agent.id) return;
    // open confirm modal instead of native confirm
    this.confirmState.set({ kind: 'section-agent', agent });
  }

  deletePageAgentConfirmed(agent: any): void {
    const agentId = agent.id as number;
    this.researchService.deletePageAgent(agentId).subscribe({
      next: () => {
        this.pageAgents.update(list => list.filter(a => a.id !== agentId));
        this.agentRuns.update(state => { const copy = { ...state }; delete copy[agentId]; return copy; });
        this.toastService.showSuccess('Agent detached and past runs removed.');
      },
      error: (err: any) => {
        const message = err?.result?.detail || err?.message || 'Unable to delete page agent.';
        this.pageError.set(message);
        this.toastService.showError(message);
      }
    });
  }

  deleteSectionAgentConfirmed(agent: any): void {
    const sectionAgentId = agent.id as number;
    this.researchService.deleteSectionAgent(sectionAgentId).subscribe({
      next: () => {
        const sectionEntry = Object.entries(this.sectionAgents()).find(([_k, v]) => v.some(a => a.id === sectionAgentId));
        if (sectionEntry) {
          const sectionId = Number(sectionEntry[0]);
          this.sectionAgents.update(s => ({ ...s, [sectionId]: (s[sectionId] || []).filter(a => a.id !== sectionAgentId) }));
        }
        this.sectionAgentRuns.update(s => { const copy = { ...s }; delete copy[sectionAgentId]; return copy; });
        this.toastService.showSuccess('Section agent detached and past runs removed.');
      },
      error: (err: any) => {
        const message = err?.result?.detail || err?.message || 'Unable to delete section agent.';
        this.pageError.set(message);
        this.toastService.showError(message);
      }
    });
  }

  cancelConfirm(): void {
    this.confirmState.set(null);
  }

  runAllAgents(): void {
    this.researchService.runAllAgents(this.pageId).subscribe({
      next: () => this.loadPage(),
      error: err => this.pageError.set(err?.result?.detail || err?.message || 'Unable to run all agents.')
    });
  }
  private loadRuns(pageAgentId: number): void {
    this.agentRunsLoading.update(state => ({ ...state, [pageAgentId]: true }));
    this.researchService.getLatestAgentRun(pageAgentId).subscribe({
      next: run => {
        this.agentRuns.update(state => ({ ...state, [pageAgentId]: run }));
        this.agentRunsLoading.update(state => ({ ...state, [pageAgentId]: false }));
      },
      error: () => this.agentRunsLoading.update(state => ({ ...state, [pageAgentId]: false }))
    });
  }

  private loadSectionAgents(sectionId: number): void {
    this.researchService.getSectionAgents(sectionId).subscribe({
      next: (agents: PageAgentResponse[]) => {
        this.sectionAgents.update(state => ({ ...state, [sectionId]: agents }));
        agents.forEach((agent: PageAgentResponse) => {
          if (agent.id !== undefined && agent.id !== null) {
            this.loadSectionRuns(agent.id);
          }
        });
      },
      error: (err: any) => this.pageError.set(err?.result?.detail || err?.message || 'Unable to load section agents.')
    });
  }

  attachSectionAgent(sectionId: number): void {
    const state = this.sectionAttachState()[sectionId] || { selectedAgentId: null, isAttaching: false, error: null };
    const selectedAgentId = state.selectedAgentId;
    if (!selectedAgentId) {
      this.sectionAttachState.update(states => ({ ...states, [sectionId]: { ...state, error: 'Choose an agent to attach.' } }));
      return;
    }
    this.sectionAttachState.update(states => ({ ...states, [sectionId]: { ...state, isAttaching: true, error: null } }));

    this.researchService.attachSectionAgent(sectionId, selectedAgentId).subscribe({
      next: (agent: PageAgentResponse) => {
        this.sectionAgents.update(state => ({
          ...state,
          [sectionId]: [...(state[sectionId] || []), agent]
        }));
        this.sectionAttachState.update(states => ({ ...states, [sectionId]: { selectedAgentId: null, isAttaching: false, error: null } }));
        if (agent.id !== undefined && agent.id !== null) {
          this.loadSectionRuns(agent.id);
        }
      },
      error: (err: any) => {
        const message = err?.result?.detail || err?.message || 'Unable to attach agent.';
        this.sectionAttachState.update(states => ({ ...states, [sectionId]: { ...state, isAttaching: false, error: message } }));
      }
    });
  }

  toggleSectionAgent(sectionAgent: SectionAgentResponse): void {
    if (!sectionAgent.id) return;
    this.researchService.toggleSectionAgent(sectionAgent.id, !sectionAgent.is_enabled).subscribe({
      next: (updated: PageAgentResponse) => {
        this.sectionAgents.update(state => ({
          ...state,
          [updated.id!]: (state[updated.id!] || []).map(a => a.id === updated.id ? updated : a)
        }));
      },
      error: (err: any) => {
        const message = err?.result?.detail || err?.message || 'Unable to update agent state.';
        this.pageError.set(message);
      }
    });
  }

  runSectionAgentNow(sectionAgent: SectionAgentResponse): void {
    if (!sectionAgent.id) return;
    this.researchService.runSectionAgent(sectionAgent.id).subscribe({
      next: (run: AgentRunResponse) => {
        // refresh runs for this agent
        this.loadSectionRuns(sectionAgent.id!);
        // update section agent quick state
        this.sectionAgents.update(state => ({
          ...state,
          [sectionAgent.id!]: (state[sectionAgent.id!] || []).map(a => a.id === sectionAgent.id ? Object.assign(a, { last_run_status: run.status, last_run_at: run.completed_at }) : a)
        }));
      },
      error: (err: any) => {
        const message = err?.result?.detail || err?.message || 'Unable to run agent.';
        this.pageError.set(message);
      }
    });
  }

  private loadSectionRuns(sectionAgentId: number): void {
    this.sectionAgentRunsLoading.update(state => ({ ...state, [sectionAgentId]: true }));
    this.researchService.getLatestSectionAgentRun(sectionAgentId).subscribe({
      next: (run: AgentRunResponse | null) => {
        this.sectionAgentRuns.update(state => ({ ...state, [sectionAgentId]: run }));
        this.sectionAgentRunsLoading.update(state => ({ ...state, [sectionAgentId]: false }));
      },
      error: (err: any) => {
        this.sectionAgentRunsLoading.update(state => ({ ...state, [sectionAgentId]: false }));
      }
    });
  }

  getSectionAttachState(sectionId: number): { selectedAgentId: number | null; isAttaching: boolean; error: string | null } {
    return this.sectionAttachState()[sectionId] || { selectedAgentId: null, isAttaching: false, error: null };
  }

  setSectionAttachSelectedAgentId(sectionId: number, agentId: number | null): void {
    this.sectionAttachState.update(states => ({
      ...states,
      [sectionId]: { ...this.getSectionAttachState(sectionId), selectedAgentId: agentId, error: null }
    }));
  }

  updateSectionAttachState(sectionId: number, updates: Partial<{ selectedAgentId: number | null; isAttaching: boolean; error: string | null }>): void {
    this.sectionAttachState.update(states => ({
      ...states,
      [sectionId]: { ...this.getSectionAttachState(sectionId), ...updates }
    }));
  }

  getSectionAgentsFor(sectionId: number): SectionAgentResponse[] {
    return this.sectionAgents()[sectionId] || [];
  }

  getSectionAgentRun(sectionAgentId: number): AgentRunResponse | null {
    return this.sectionAgentRuns()[sectionAgentId] || null;
  }

  isSectionAgentRunLoading(sectionAgentId: number): boolean {
    return this.sectionAgentRunsLoading()[sectionAgentId] || false;
  }

  getUnattachedAgentsForSection(sectionId: number): AvailableAgentResponse[] {
    const attachedIds = new Set(this.getSectionAgentsFor(sectionId).map(a => a.agent_id));
    return this.availableAgents().filter(a => !attachedIds.has(a.id));
  }

  trackBySection = (_: number, section: SectionResponse) => section.id;
  trackByComment = (_: number, comment: CommentResponse) => comment.id;
  trackByAgent = (_: number, agent: PageAgentResponse) => agent.id;
  trackByAvailableAgent = (_: number, agent: AvailableAgentResponse) => agent.id;
  trackByRun = (_: number, run: AgentRunResponse) => run.id;

  /** Convert a small subset of Markdown -> sanitized HTML for display */
  private markdownToHtml(markdown: string): string {
    if (!markdown) return '';

    const escapeHtml = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

    // escape first to avoid any embedded HTML
    let html = escapeHtml(markdown);

    // code blocks ```...``` (preserve formatting)
    html = html.replace(/```([\s\S]*?)```/g, (_m, code) => `<pre><code>${escapeHtml(code)}</code></pre>`);

    // inline code `...`
    html = html.replace(/`([^`]+)`/g, '<code>$1</code>');

    // headings
    html = html.replace(/^### (.*$)/gm, '<h3>$1</h3>');
    html = html.replace(/^## (.*$)/gm, '<h2>$1</h2>');
    html = html.replace(/^# (.*$)/gm, '<h1>$1</h1>');

    // bold and italics
    html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    html = html.replace(/(^|[^*])\*([^*]+)\*([^*]|$)/g, (_m, p1, p2, p3) => `${p1}<em>${p2}</em>${p3}`);

    // links [text](url)
    html = html.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>');

    // paragraphs (double newlines) – detect tables vs plain paragraphs
    const parseTableRow = (row: string) =>
      row.trim().replace(/^\||\|$/g, '').split('|').map(cell => cell.trim());

    html = html.split(/\n\s*\n/).map(block => {
      const lines = block.trim().split('\n');
      // Table: first line is a pipe row, second line is a separator row (|---|---|)
      if (
        lines.length >= 2 &&
        /^\|.+\|$/.test(lines[0].trim()) &&
        /^\|[\s\-:|\s]+\|$/.test(lines[1].trim())
      ) {
        const headers = parseTableRow(lines[0]);
        const dataRows = lines.slice(2).filter(l => l.trim());
        const thead = `<thead><tr>${headers.map(h => `<th>${h}</th>`).join('')}</tr></thead>`;
        const tbody = dataRows.length
          ? `<tbody>${dataRows.map(r => `<tr>${parseTableRow(r).map(c => `<td>${c}</td>`).join('')}</tr>`).join('')}</tbody>`
          : '';
        return `<table>${thead}${tbody}</table>`;
      }
      return `<p>${block.replace(/\n/g, '<br/>')}</p>`;
    }).join('');

    return html;
  }

  toHtml(markdown?: string): string {
    return markdown ? this.markdownToHtml(markdown) : '';
  }

  toggleCommentExpand(commentId: number): void {
    const expanded = new Set(this.expandedComments());
    if (expanded.has(commentId)) {
      expanded.delete(commentId);
    } else {
      expanded.add(commentId);
    }
    this.expandedComments.set(expanded);
  }

  isCommentExpanded(commentId: number): boolean {
    return this.expandedComments().has(commentId);
  }

  addNewSection(): void {
    const title = prompt('Enter section title:');
    if (title && title.trim()) {
      this.researchService.createSection(this.pageId, title.trim()).subscribe({
        next: newSection => {
          // Reload the page to get updated sections
          this.loadPage();
          this.toastService.showSuccess('Section added successfully');
        },
        error: err => {
          const message = err?.result?.detail || err?.message || 'Failed to add section';
          this.toastService.showError(message);
        }
      });
    }
  }

  deleteSection(section: any): void {
    this.confirmState.set({ kind: 'section', section });
  }

  onConfirmDelete(): void {
    const state = this.confirmState();
    if (!state) return;

    if (state.kind === 'section' && state.section) {
      this.researchService.deleteSection(state.section.id).subscribe({
        next: () => {
          this.loadPage();
          this.toastService.showSuccess('Section deleted successfully');
        },
        error: err => {
          const message = err?.result?.detail || err?.message || 'Failed to delete section';
          this.toastService.showError(message);
        }
      });
    } else if (state.kind === 'page-agent' && state.agent) {
      this.deletePageAgentConfirmed(state.agent);
    } else if (state.kind === 'section-agent' && state.agent) {
      this.deleteSectionAgentConfirmed(state.agent);
    }

    this.confirmState.set(null);
  }
}

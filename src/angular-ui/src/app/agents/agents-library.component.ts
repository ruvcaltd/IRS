import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AgentsService } from './agents.service';
import { LlmService } from '../llm/llm.service';
import { AvailableAgentResponse, CreateAgentRequest, LlmModelResponse } from '../../generated/generated/irs-api.client';

@Component({
  selector: 'app-agents-library',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './agents-library.component.html',
  styleUrl: './agents-library.component.scss'
})
export class AgentsLibraryComponent {
  teamId = Number(new URLSearchParams(window.location.search).get('') || '0');
  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly agentsService: AgentsService,
    private readonly llmService: LlmService
  ) {
    this.teamId = Number(this.route.snapshot.paramMap.get('id'));
    this.loadAgents();
    this.loadLlmModels();
  }

  state = signal({
    agents: [] as AvailableAgentResponse[],
    llmModels: [] as LlmModelResponse[],
    isLoading: false,
    error: null as string | null,
    notice: null as string | null
  });

  form = signal({
    name: '',
    description: '',
    visibility: 'Private',
    endpoint_url: '',
    http_method: 'GET',
    auth_type: 'None',
    username: '',
    password: '',
    api_token: '',
    login_endpoint_url: '',
    request_body_template: '',
    agent_instructions: '',
    response_mapping: '',
    llm_model_id: null as number | null,
    llm_api_key: ''
  });

  // id of the agent currently being edited (null = create mode)
  editingAgentId = signal<number | null>(null);
  // Track if the editing agent has an API key set (for UI indication)
  editingAgentHasApiKey = signal<boolean>(false);

  agents = computed(() => this.state().agents);
  llmModels = computed(() => this.state().llmModels);
  isLoading = computed(() => this.state().isLoading);
  error = computed(() => this.state().error);
  notice = computed(() => this.state().notice);

  loadAgents(): void {
    this.state.update(s => ({ ...s, isLoading: true, error: null }));
    this.agentsService.getAvailableAgents(this.teamId).subscribe({
      next: agents => this.state.update(s => ({ ...s, agents, isLoading: false })),
      error: err => this.state.update(s => ({ ...s, agents: [], isLoading: false, error: err?.message ?? 'Unable to load agents' }))
    });
  }

  loadLlmModels(): void {
    this.llmService.getModels(undefined).subscribe({
      next: models => {
        this.state.update(s => ({ ...s, llmModels: models }));
      },
      error: err => console.error('Failed to load LLM models', err)
    });
  }

  startEdit(agent: AvailableAgentResponse): void {
    // Load agent details then populate the form
    this.state.update(s => ({ ...s, isLoading: true, error: null }));
    this.agentsService.getAgent(this.teamId, agent.id!).subscribe({
      next: detail => {
        if (!detail) {
          this.state.update(s => ({ ...s, isLoading: false, error: 'Unable to load agent details' }));
          return;
        }

        this.editingAgentId.set(detail.id ?? null);
        this.editingAgentHasApiKey.set(detail.has_llm_api_key ?? false);
        this.form.set({
          name: detail.name ?? '',
          description: detail.description ?? '',
          visibility: detail.visibility ?? 'Private',
          endpoint_url: detail.endpoint_url ?? '',
          http_method: detail.http_method ?? 'GET',
          auth_type: detail.auth_type ?? 'None',
          username: detail.username ?? '',
          password: '', // do not pre-fill secret
          api_token: '', // do not pre-fill secret
          login_endpoint_url: detail.login_endpoint_url ?? '',
          request_body_template: detail.request_body_template ?? '',
          agent_instructions: detail.agent_instructions ?? '',
          response_mapping: detail.response_mapping ?? '',
          llm_model_id: detail.llm_model_id ?? null,
          llm_api_key: '' // do not pre-fill secret
        });

        this.state.update(s => ({ ...s, isLoading: false }));
      },
      error: err => this.state.update(s => ({ ...s, isLoading: false, error: err?.message || 'Failed to load agent details' }))
    });
  }

  /**
   * Clone an existing agent into the create form so user can modify and save as new.
   * Secrets (password/api_token/llm_api_key) are not copied.
   */
  private _noticeTimer = 0 as any;

  private showNotice(message: string, durationMs = 3000) {
    // clear existing timer
    if (this._noticeTimer) {
      clearTimeout(this._noticeTimer);
      this._noticeTimer = 0;
    }

    this.state.update(s => ({ ...s, notice: message }));
    this._noticeTimer = window.setTimeout(() => this.state.update(s => ({ ...s, notice: null })), durationMs);
  }

  cloneAgent(agent: AvailableAgentResponse): void {
    this.state.update(s => ({ ...s, isLoading: true, error: null }));
    this.agentsService.getAgent(this.teamId, agent.id!).subscribe({
      next: detail => {
        if (!detail) {
          this.state.update(s => ({ ...s, isLoading: false, error: 'Unable to load agent details for clone' }));
          return;
        }

        // Switch to create mode (null = create)
        this.editingAgentId.set(null);
        this.editingAgentHasApiKey.set(false);

        // Prefill form with agent details, but clear secrets and tweak name
        this.form.set({
          name: (detail.name ? detail.name + ' (copy)' : 'Copy of agent'),
          description: detail.description ?? '',
          visibility: detail.visibility ?? 'Private',
          endpoint_url: detail.endpoint_url ?? '',
          http_method: detail.http_method ?? 'GET',
          auth_type: detail.auth_type ?? 'None',
          username: detail.username ?? '',
          password: '',
          api_token: '',
          login_endpoint_url: detail.login_endpoint_url ?? '',
          request_body_template: detail.request_body_template ?? '',
          agent_instructions: detail.agent_instructions ?? '',
          response_mapping: detail.response_mapping ?? '',
          llm_model_id: detail.llm_model_id ?? null,
          llm_api_key: ''
        });

        this.state.update(s => ({ ...s, isLoading: false }));

        // give a small confirmation toast/notice
        this.showNotice('Agent copied into create form — modify and click Create.');

        // Scroll to top of the form so user sees the create fields
        window.scrollTo({ top: 0, behavior: 'smooth' });
      },
      error: err => this.state.update(s => ({ ...s, isLoading: false, error: err?.message || 'Failed to load agent details for clone' }))
    });
  }

  cancelEdit(): void {
    this.editingAgentId.set(null);
    this.editingAgentHasApiKey.set(false);
    this.form.set({
      name: '',
      description: '',
      visibility: 'Private',
      endpoint_url: '',
      http_method: 'GET',
      auth_type: 'None',
      username: '',
      password: '',
      api_token: '',
      login_endpoint_url: '',
      request_body_template: '',
      agent_instructions: '',
      response_mapping: '',
      llm_model_id: null,
      llm_api_key: ''
    });
  }

  createAgent(): void {
    const formData = this.form();
    if (!formData.name || !formData.endpoint_url || !formData.agent_instructions) {
      this.state.update(s => ({ ...s, error: 'Name, endpoint URL, and agent instructions are required.' }));
      return;
    }

    const payload = new CreateAgentRequest({
      name: formData.name,
      description: formData.description,
      visibility: formData.visibility,
      endpoint_url: formData.endpoint_url,
      http_method: formData.http_method,
      auth_type: formData.auth_type,
      username: formData.username,
      password: formData.password,
      api_token: formData.api_token,
      login_endpoint_url: formData.login_endpoint_url,
      request_body_template: formData.request_body_template,
      agent_instructions: formData.agent_instructions,
      response_mapping: formData.response_mapping,
      llm_model_id: formData.llm_model_id ?? undefined,
      llm_api_key: formData.llm_api_key
    });

    this.state.update(s => ({ ...s, isLoading: true, error: null }));

    // If editing an existing agent, call update endpoint
    if (this.editingAgentId() !== null) {
      const agentId = this.editingAgentId()!;
      this.agentsService.updateAgent(this.teamId, agentId, payload).subscribe({
        next: agent => {
          if (agent) {
            this.state.update(s => ({ ...s, agents: s.agents.map(a => a.id === agent.id ? agent : a), isLoading: false }));
            this.cancelEdit();
          }
        },
        error: err => this.state.update(s => ({ ...s, isLoading: false, error: err?.result?.detail || err?.message || 'Unable to update agent.' }))
      });
      return;
    }

    // Create new agent
    this.agentsService.createAgent(this.teamId, payload).subscribe({
      next: agent => {
        if (agent) {
          this.state.update(s => ({ ...s, agents: [...s.agents, agent], isLoading: false }));
          this.cancelEdit(); // Reset form
        }
      },
      error: err => this.state.update(s => ({ ...s, isLoading: false, error: err?.result?.detail || err?.message || 'Unable to create agent.' }))
    });
  }
}
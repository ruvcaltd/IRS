import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators, FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { TeamsService } from './teams.service';

@Component({
  selector: 'app-team-create',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './team-create.component.html',
  styleUrl: './team-create.component.scss'
})
export class TeamCreateComponent {
  form: FormGroup;

  constructor(
    private readonly fb: FormBuilder,
    private readonly router: Router,
    private readonly teamsService: TeamsService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(2)]]
    });
  }

  state = signal<{ isSubmitting: boolean; error: string | null }>({ isSubmitting: false, error: null });
  isSubmitting = computed(() => this.state().isSubmitting);
  error = computed(() => this.state().error);

  onSubmit(): void {
    if (this.form.invalid || this.isSubmitting()) return;
    const name = this.form.value.name ?? '';

    this.state.set({ isSubmitting: true, error: null });
    this.teamsService.createTeam(name).subscribe({
      next: team => {
        this.state.set({ isSubmitting: false, error: null });
        this.router.navigate(['/teams', team.id]);
      },
      error: err => {
        const message = err?.result?.detail || err?.message || 'Failed to create team.';
        this.state.set({ isSubmitting: false, error: message });
      }
    });
  }
}

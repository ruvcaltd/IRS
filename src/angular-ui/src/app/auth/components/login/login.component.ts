import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { AuthStoreService } from '../../services/auth-store.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly authStore = inject(AuthStoreService);

  loginForm: FormGroup;
  isSubmitted = false;

  constructor() {
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  get email() {
    return this.loginForm.get('email');
  }

  get password() {
    return this.loginForm.get('password');
  }

  isLoading = this.authStore.isLoading;
  error = this.authStore.error;

  onSubmit(): void {
    this.isSubmitted = true;

    if (this.loginForm.invalid) {
      return;
    }

    this.authStore.setLoading(true);

    const { email, password } = this.loginForm.value;

    this.authService.login(email, password).subscribe({
      next: (response) => {
        this.authStore.setUser(response);
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        const errorMessage = error?.result?.detail || error?.message || 'Login failed. Please try again.';
        this.authStore.setError(errorMessage);
      }
    });
  }

  getEmailErrors(): string[] {
    const control = this.email;
    if (!control || !control.errors) {
      return [];
    }

    const errors: string[] = [];
    if (control.errors['required']) {
      errors.push('Email is required');
    }
    if (control.errors['email']) {
      errors.push('Please enter a valid email address');
    }
    return errors;
  }

  getPasswordErrors(): string[] {
    const control = this.password;
    if (!control || !control.errors) {
      return [];
    }

    const errors: string[] = [];
    if (control.errors['required']) {
      errors.push('Password is required');
    }
    if (control.errors['minlength']) {
      errors.push('Password must be at least 6 characters');
    }
    return errors;
  }
}

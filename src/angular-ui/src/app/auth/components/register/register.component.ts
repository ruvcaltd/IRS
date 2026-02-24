import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../services/auth.service';
import { AuthStoreService } from '../../services/auth-store.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  private readonly authStore = inject(AuthStoreService);

  registerForm: FormGroup;
  isSubmitted = false;

  constructor() {
    this.registerForm = this.fb.group(
      {
        email: ['', [Validators.required, Validators.email]],
        fullName: ['', [Validators.required, Validators.minLength(2)]],
        password: ['', [Validators.required, Validators.minLength(8)]],
        confirmPassword: ['', Validators.required]
      },
      { validators: this.passwordMatchValidator }
    );
  }

  get email() {
    return this.registerForm.get('email');
  }

  get fullName() {
    return this.registerForm.get('fullName');
  }

  get password() {
    return this.registerForm.get('password');
  }

  get confirmPassword() {
    return this.registerForm.get('confirmPassword');
  }

  isLoading = this.authStore.isLoading;
  error = this.authStore.error;

  /**
   * Validate that passwords match
   */
  private passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
    const password = control.get('password');
    const confirmPassword = control.get('confirmPassword');

    if (!password || !confirmPassword) {
      return null;
    }

    return password.value === confirmPassword.value ? null : { passwordMismatch: true };
  }

  onSubmit(): void {
    this.isSubmitted = true;

    if (this.registerForm.invalid) {
      return;
    }

    this.authStore.setLoading(true);

    const { email, fullName, password } = this.registerForm.value;

    this.authService.register(email, password, fullName).subscribe({
      next: (response) => {
        this.authStore.setUser(response);
        this.router.navigate(['/dashboard']);
      },
      error: (error) => {
        const errorMessage = error?.result?.detail || error?.message || 'Registration failed. Please try again.';
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

  getFullNameErrors(): string[] {
    const control = this.fullName;
    if (!control || !control.errors) {
      return [];
    }

    const errors: string[] = [];
    if (control.errors['required']) {
      errors.push('Full name is required');
    }
    if (control.errors['minlength']) {
      errors.push('Full name must be at least 2 characters');
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
      errors.push('Password must be at least 8 characters');
    }
    return errors;
  }

  getConfirmPasswordErrors(): string[] {
    const control = this.confirmPassword;
    const formErrors = this.registerForm.errors;

    if (!control || (!control.errors && !formErrors?.['passwordMismatch'])) {
      return [];
    }

    const errors: string[] = [];
    if (control?.errors?.['required']) {
      errors.push('Please confirm your password');
    }
    if (formErrors?.['passwordMismatch']) {
      errors.push('Passwords do not match');
    }
    return errors;
  }
}

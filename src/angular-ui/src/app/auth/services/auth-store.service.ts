import { Injectable, signal, computed } from '@angular/core';
import { AuthService } from './auth.service';
import { AuthResponse } from '../../../generated/generated/irs-api.client';

export interface AuthState {
  user: AuthResponse | null;
  isLoading: boolean;
  error: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class AuthStoreService {
  // Signals
  private readonly authState = signal<AuthState>({
    user: null,
    isLoading: false,
    error: null
  });

  // Computed signals
  readonly user = computed(() => this.authState().user);
  readonly isLoading = computed(() => this.authState().isLoading);
  readonly error = computed(() => this.authState().error);
  readonly isAuthenticated = computed(() => this.authState().user !== null);

  constructor(private authService: AuthService) {
    this.initializeAuthState();
  }

  /**
   * Initialize auth state from stored data
   */
  private initializeAuthState(): void {
    const user = this.authService.getUser();
    if (user && this.authService.isAuthenticated()) {
      this.authState.set({
        user,
        isLoading: false,
        error: null
      });
    }
  }

  /**
   * Set loading state
   */
  setLoading(isLoading: boolean): void {
    this.authState.update((state) => ({
      ...state,
      isLoading
    }));
  }

  /**
   * Set error
   */
  setError(error: string | null): void {
    this.authState.update((state) => ({
      ...state,
      error,
      isLoading: false
    }));
  }

  /**
   * Set authenticated user
   */
  setUser(user: AuthResponse): void {
    this.authState.set({
      user,
      isLoading: false,
      error: null
    });
  }

  /**
   * Clear auth state (logout)
   */
  clearAuth(): void {
    this.authService.logout();
    this.authState.set({
      user: null,
      isLoading: false,
      error: null
    });
  }
}

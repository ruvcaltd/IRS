import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { AuthStoreService } from '../auth/services/auth-store.service';
import { ThemeService } from '../core/theme.service';
import { ToastService } from '../core/toast.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './layout.component.html',
  styleUrl: './layout.component.scss'
})
export class LayoutComponent {
  private readonly router = inject(Router);
  private readonly authStore = inject(AuthStoreService);
  private readonly themeService = inject(ThemeService);
  public readonly toastService = inject(ToastService);

  user = this.authStore.user;
  isDark = this.themeService.isDark;
  public readonly toasts = this.toastService.toasts;
  mobileMenuOpen = signal(false);

  toggleTheme(): void {
    this.themeService.toggleTheme();
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update(v => !v);
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  onLogout(): void {
    this.authStore.clearAuth();
    this.router.navigate(['/login']);
  }
}

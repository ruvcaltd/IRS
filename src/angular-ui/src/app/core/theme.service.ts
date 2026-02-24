import { Injectable, signal, effect } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  isDark = signal<boolean>(true);

  constructor() {
    // Load saved theme preference or default to dark
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme) {
      this.isDark.set(savedTheme === 'dark');
    } else {
      // Check system preference
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      this.isDark.set(prefersDark);
    }

    // Apply theme on initialization
    this.applyTheme();

    // Watch for theme changes and apply
    effect(() => {
      this.applyTheme();
    });
  }

  toggleTheme(): void {
    this.isDark.update(dark => !dark);
    localStorage.setItem('theme', this.isDark() ? 'dark' : 'light');
  }

  private applyTheme(): void {
    if (this.isDark()) {
      document.documentElement.classList.remove('light-theme');
      document.documentElement.classList.add('dark-theme');
    } else {
      document.documentElement.classList.remove('dark-theme');
      document.documentElement.classList.add('light-theme');
    }
  }
}

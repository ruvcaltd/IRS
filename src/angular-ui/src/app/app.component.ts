import { Component } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, CommonModule],
  template: `
    <nav>
      <a routerLink="/" routerLinkActive="active">Home</a>
      <a routerLink="/users" routerLinkActive="active">Users</a>
      <a routerLink="/db-test" routerLinkActive="active">DB Test</a>
    </nav>
    <main>
      <router-outlet></router-outlet>
    </main>
  `,
  styles: [`
    nav {
      background-color: #333;
      padding: 10px;
    }
    
    nav a {
      color: white;
      text-decoration: none;
      margin-right: 20px;
      padding: 8px 16px;
      border-radius: 4px;
    }
    
    nav a:hover, nav a.active {
      background-color: #555;
    }
    
    main {
      padding: 20px;
    }
  `]
})
export class AppComponent {
  title = 'angular-ui';
}

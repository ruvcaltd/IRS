import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';
import { PiClient } from '../../api-client/api-client';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="container">
      <h1>Hello World Pi Example</h1>
      <button (click)="loadPi()" [disabled]="loading()">Get Pi Value</button>
      
      @if (loading()) {
        <p>Loading...</p>
      }
      
      @if (piData(); as data) {
        <div class="result">
          <h2>{{ data.message }}</h2>
          <p><strong>Pi Value:</strong> {{ data.pi }}</p>
          <p><strong>Formatted:</strong> {{ data.piFormatted }}</p>
        </div>
      }
      
      @if (error()) {
        <div class="error">
          <p>Error: {{ error() }}</p>
        </div>
      }
    </div>
  `,
  styles: [`
    .container {
      max-width: 800px;
      margin: 0 auto;
      padding: 20px;
    }
    
    button {
      padding: 10px 20px;
      font-size: 16px;
      cursor: pointer;
      background-color: #007bff;
      color: white;
      border: none;
      border-radius: 4px;
    }
    
    button:disabled {
      background-color: #ccc;
      cursor: not-allowed;
    }
    
    .result {
      margin-top: 20px;
      padding: 15px;
      background-color: #f0f0f0;
      border-radius: 4px;
    }
    
    .error {
      margin-top: 20px;
      padding: 15px;
      background-color: #ffebee;
      color: #c62828;
      border-radius: 4px;
    }
  `]
})
export class HomeComponent {
  private readonly piClient = inject(PiClient);
  protected readonly loading = signal(false);
  protected readonly piData = signal<any>(null);
  protected readonly error = signal<string | null>(null);

  loadPi(): void {
    this.loading.set(true);
    this.error.set(null);
    
    (this.piClient.getHelloPi() as Observable<any>).subscribe({
      next: (response: any) => {
        this.piData.set(response);
        this.loading.set(false);
      },
      error: (err: any) => {
        this.error.set(err?.message || 'Failed to load Pi value');
        this.loading.set(false);
      }
    });
  }
}

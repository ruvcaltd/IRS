import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UsersClient, UserDto, CreateUserDto } from '../../api-client/api-client';

interface TestResult {
  name: string;
  status: 'pending' | 'success' | 'error';
  message: string;
  timestamp?: Date;
}

@Component({
  selector: 'app-db-test',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <h1>Database Connectivity Test</h1>
      
      <div class="test-controls">
        <button (click)="runAllTests()" [disabled]="running()" class="btn-primary">
          {{ running() ? 'Running Tests...' : 'Run All Tests' }}
        </button>
        <button (click)="clearResults()" [disabled]="running()" class="btn-secondary">
          Clear Results
        </button>
      </div>

      <div class="test-results">
        <h2>Test Results</h2>
        
        @for (result of testResults(); track result.name) {
          <div class="test-item" [class.success]="result.status === 'success'" [class.error]="result.status === 'error'" [class.pending]="result.status === 'pending'">
            <div class="test-header">
              <span class="test-name">{{ result.name }}</span>
              <span class="test-status">
                @if (result.status === 'success') {
                  ✓ Success
                } @else if (result.status === 'error') {
                  ✗ Failed
                } @else {
                  ⏳ Running...
                }
              </span>
            </div>
            <div class="test-message">{{ result.message }}</div>
            @if (result.timestamp) {
              <div class="test-timestamp">{{ result.timestamp | date:'medium' }}</div>
            }
          </div>
        }
      </div>

      <div class="manual-test">
        <h2>Manual Test - Create User</h2>
        <form (ngSubmit)="createTestUser()">
          <input 
            type="text" 
            [(ngModel)]="testUserName" 
            name="userName"
            placeholder="Enter test user name"
            [disabled]="running()"
            required
          />
          <button type="submit" [disabled]="running() || !testUserName">Create Test User</button>
        </form>
        @if (lastCreatedUser()) {
          <div class="success-message">
            ✓ User created: {{ lastCreatedUser()?.userName }} (ID: {{ lastCreatedUser()?.id }})
          </div>
        }
      </div>

      <div class="summary">
        <h2>Summary</h2>
        <div class="summary-stats">
          <div class="stat">
            <span class="stat-label">Total Tests:</span>
            <span class="stat-value">{{ testResults().length }}</span>
          </div>
          <div class="stat success">
            <span class="stat-label">Passed:</span>
            <span class="stat-value">{{ passedCount() }}</span>
          </div>
          <div class="stat error">
            <span class="stat-label">Failed:</span>
            <span class="stat-value">{{ failedCount() }}</span>
          </div>
        </div>
        @if (testResults().length > 0 && failedCount() === 0) {
          <div class="all-passed">
            🎉 All tests passed! Database connectivity is working correctly.
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .container {
      max-width: 900px;
      margin: 0 auto;
      padding: 20px;
    }

    h1 {
      color: #333;
      margin-bottom: 30px;
    }

    h2 {
      color: #555;
      margin-top: 30px;
      margin-bottom: 15px;
      font-size: 1.3em;
    }

    .test-controls {
      display: flex;
      gap: 10px;
      margin-bottom: 30px;
    }

    button {
      padding: 10px 20px;
      font-size: 14px;
      cursor: pointer;
      border: none;
      border-radius: 4px;
      font-weight: 500;
    }

    button:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-primary {
      background-color: #007bff;
      color: white;
    }

    .btn-primary:hover:not(:disabled) {
      background-color: #0056b3;
    }

    .btn-secondary {
      background-color: #6c757d;
      color: white;
    }

    .btn-secondary:hover:not(:disabled) {
      background-color: #545b62;
    }

    .test-results {
      margin-bottom: 30px;
    }

    .test-item {
      padding: 15px;
      margin-bottom: 10px;
      border-radius: 4px;
      border-left: 4px solid #ccc;
      background-color: #f8f9fa;
    }

    .test-item.pending {
      border-left-color: #ffc107;
      background-color: #fff3cd;
    }

    .test-item.success {
      border-left-color: #28a745;
      background-color: #d4edda;
    }

    .test-item.error {
      border-left-color: #dc3545;
      background-color: #f8d7da;
    }

    .test-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
    }

    .test-name {
      font-weight: 600;
      font-size: 16px;
    }

    .test-status {
      font-weight: 500;
    }

    .test-item.success .test-status {
      color: #155724;
    }

    .test-item.error .test-status {
      color: #721c24;
    }

    .test-item.pending .test-status {
      color: #856404;
    }

    .test-message {
      color: #666;
      margin-bottom: 5px;
    }

    .test-timestamp {
      font-size: 12px;
      color: #999;
    }

    .manual-test {
      padding: 20px;
      background-color: #f8f9fa;
      border-radius: 4px;
      margin-bottom: 30px;
    }

    .manual-test form {
      display: flex;
      gap: 10px;
      margin-top: 10px;
    }

    .manual-test input {
      flex: 1;
      padding: 8px;
      font-size: 14px;
      border: 1px solid #ddd;
      border-radius: 4px;
    }

    .manual-test button {
      background-color: #28a745;
      color: white;
    }

    .manual-test button:hover:not(:disabled) {
      background-color: #218838;
    }

    .success-message {
      margin-top: 10px;
      padding: 10px;
      background-color: #d4edda;
      color: #155724;
      border-radius: 4px;
      border-left: 4px solid #28a745;
    }

    .summary {
      padding: 20px;
      background-color: #e9ecef;
      border-radius: 4px;
    }

    .summary-stats {
      display: flex;
      gap: 30px;
      margin-bottom: 15px;
    }

    .stat {
      display: flex;
      flex-direction: column;
      gap: 5px;
    }

    .stat-label {
      font-size: 12px;
      color: #666;
      text-transform: uppercase;
    }

    .stat-value {
      font-size: 24px;
      font-weight: bold;
      color: #333;
    }

    .stat.success .stat-value {
      color: #28a745;
    }

    .stat.error .stat-value {
      color: #dc3545;
    }

    .all-passed {
      padding: 15px;
      background-color: #d4edda;
      color: #155724;
      border-radius: 4px;
      border-left: 4px solid #28a745;
      font-weight: 500;
      margin-top: 15px;
    }
  `]
})
export class DbTestComponent {
  private readonly usersClient = inject(UsersClient);
  protected readonly running = signal(false);
  protected readonly testResults = signal<TestResult[]>([]);
  protected readonly lastCreatedUser = signal<UserDto | null>(null);
  protected testUserName = '';

  protected passedCount = computed(() => 
    this.testResults().filter((r: TestResult) => r.status === 'success').length
  );
  
  protected failedCount = computed(() => 
    this.testResults().filter((r: TestResult) => r.status === 'error').length
  );

  runAllTests(): void {
    this.running.set(true);
    this.testResults.set([]);

    // Test 1: Database Connection - GET Users
    this.addTestResult({
      name: 'Database Connection Test',
      status: 'pending',
      message: 'Testing database connection...'
    });

    this.usersClient.getUsers().subscribe({
      next: (users: UserDto[]) => {
        this.updateTestResult('Database Connection Test', 'success', 
          `Successfully connected to database. Found ${users.length} user(s).`);
        this.running.set(false);
      },
      error: (err: any) => {
        this.updateTestResult('Database Connection Test', 'error', 
          `Failed to connect: ${err?.message || 'Unknown error'}`);
        this.running.set(false);
      }
    });

    // Test 2: Create User
    setTimeout(() => {
      this.addTestResult({
        name: 'Create User Test',
        status: 'pending',
        message: 'Testing user creation...'
      });

      const testUser: CreateUserDto = {
        userName: `TestUser_${Date.now()}`
      };

      this.usersClient.createUser(testUser).subscribe({
        next: (user: UserDto) => {
          this.updateTestResult('Create User Test', 'success', 
            `User created successfully: ${user.userName} (ID: ${user.id})`);
          
          // Test 3: Read User
          setTimeout(() => {
            this.addTestResult({
              name: 'Read User Test',
              status: 'pending',
              message: 'Testing user retrieval...'
            });

            this.usersClient.getUsers().subscribe({
              next: (users: UserDto[]) => {
                const foundUser = users.find(u => u.id === user.id);
                if (foundUser) {
                  this.updateTestResult('Read User Test', 'success', 
                    `User retrieved successfully: ${foundUser.userName}`);
                } else {
                  this.updateTestResult('Read User Test', 'error', 
                    'Created user not found in database');
                }
              },
              error: (err: any) => {
                this.updateTestResult('Read User Test', 'error', 
                  `Failed to retrieve users: ${err?.message || 'Unknown error'}`);
              }
            });

            // Test 4: Delete User
            setTimeout(() => {
              this.addTestResult({
                name: 'Delete User Test',
                status: 'pending',
                message: 'Testing user deletion...'
              });

              this.usersClient.deleteUser(user.id).subscribe({
                next: () => {
                  this.updateTestResult('Delete User Test', 'success', 
                    `User deleted successfully (ID: ${user.id})`);
                },
                error: (err: any) => {
                  this.updateTestResult('Delete User Test', 'error', 
                    `Failed to delete user: ${err?.message || 'Unknown error'}`);
                }
              });
            }, 500);
          }, 500);
        },
        error: (err: any) => {
          this.updateTestResult('Create User Test', 'error', 
            `Failed to create user: ${err?.message || 'Unknown error'}`);
        }
      });
    }, 500);
  }

  createTestUser(): void {
    if (!this.testUserName.trim()) {
      return;
    }

    this.running.set(true);
    this.lastCreatedUser.set(null);

    const createUserDto: CreateUserDto = {
      userName: this.testUserName.trim()
    };

    this.usersClient.createUser(createUserDto).subscribe({
      next: (user: UserDto) => {
        this.lastCreatedUser.set(user);
        this.testUserName = '';
        this.running.set(false);
      },
      error: (err: any) => {
        alert(`Failed to create user: ${err?.message || 'Unknown error'}`);
        this.running.set(false);
      }
    });
  }

  clearResults(): void {
    this.testResults.set([]);
    this.lastCreatedUser.set(null);
  }

  private addTestResult(result: TestResult): void {
    result.timestamp = new Date();
    this.testResults.update(results => [...results, result]);
  }

  private updateTestResult(name: string, status: 'success' | 'error', message: string): void {
    this.testResults.update(results => 
      results.map(r => 
        r.name === name 
          ? { ...r, status, message, timestamp: new Date() }
          : r
      )
    );
  }
}

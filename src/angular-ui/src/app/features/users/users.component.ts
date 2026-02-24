import { Component, inject, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Observable } from 'rxjs';
import { UsersClient, UserDto, CreateUserDto } from '../../api-client/api-client';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="container">
      <h1>Users Management</h1>
      
      <div class="add-user">
        <h2>Add New User</h2>
        <form (ngSubmit)="addUser()">
          <input 
            type="text" 
            [(ngModel)]="newUserName" 
            name="userName"
            placeholder="Enter user name"
            required
            [disabled]="loading()"
          />
          <button type="submit" [disabled]="loading() || !newUserName">Add User</button>
        </form>
      </div>
      
      <div class="users-list">
        <h2>Users List</h2>
        <button (click)="loadUsers()" [disabled]="loading()">Refresh</button>
        
        @if (loading()) {
          <p>Loading...</p>
        }
        
        @if (users().length === 0 && !loading()) {
          <p>No users found.</p>
        }
        
        <ul>
          @for (user of users(); track user.id) {
            <li>
              <span>{{ user.userName }}</span>
              <button (click)="deleteUser(user.id)" [disabled]="loading()">Delete</button>
            </li>
          }
        </ul>
      </div>
      
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
    
    .add-user, .users-list {
      margin-bottom: 30px;
      padding: 20px;
      background-color: #f9f9f9;
      border-radius: 4px;
    }
    
    form {
      display: flex;
      gap: 10px;
      margin-top: 10px;
    }
    
    input {
      flex: 1;
      padding: 8px;
      font-size: 14px;
      border: 1px solid #ddd;
      border-radius: 4px;
    }
    
    button {
      padding: 8px 16px;
      font-size: 14px;
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
    
    button:hover:not(:disabled) {
      background-color: #0056b3;
    }
    
    ul {
      list-style: none;
      padding: 0;
      margin-top: 15px;
    }
    
    li {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 10px;
      margin-bottom: 8px;
      background-color: white;
      border-radius: 4px;
      border: 1px solid #ddd;
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
export class UsersComponent implements OnInit {
  private readonly usersClient = inject(UsersClient);
  protected readonly loading = signal(false);
  protected readonly users = signal<UserDto[]>([]);
  protected readonly error = signal<string | null>(null);
  protected newUserName = '';

  ngOnInit(): void {
    this.loadUsers();
  }

  loadUsers(): void {
    this.loading.set(true);
    this.error.set(null);
    
    (this.usersClient.getUsers() as Observable<UserDto[]>).subscribe({
      next: (response: UserDto[]) => {
        this.users.set(response);
        this.loading.set(false);
      },
      error: (err: any) => {
        this.error.set(err?.message || 'Failed to load users');
        this.loading.set(false);
      }
    });
  }

  addUser(): void {
    if (!this.newUserName.trim()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    
    const createUserDto: CreateUserDto = {
      userName: this.newUserName.trim()
    };
    
    (this.usersClient.createUser(createUserDto) as Observable<UserDto>).subscribe({
      next: () => {
        this.newUserName = '';
        this.loadUsers();
      },
      error: (err: any) => {
        this.error.set(err?.message || 'Failed to create user');
        this.loading.set(false);
      }
    });
  }

  deleteUser(id: number): void {
    if (!confirm('Are you sure you want to delete this user?')) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    
    (this.usersClient.deleteUser(id) as Observable<void>).subscribe({
      next: () => {
        this.loadUsers();
      },
      error: (err: any) => {
        this.error.set(err?.message || 'Failed to delete user');
        this.loading.set(false);
      }
    });
  }
}

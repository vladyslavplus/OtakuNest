import { Component, OnInit } from '@angular/core';
import { ApplicationUser } from '../../features/user/models/application-user.model';
import { UpdateUserDto } from '../../features/user/models/update-user.dto';
import { UserService } from '../../features/user/services/user.service';
import { AuthService } from '../../features/user/services/auth.service';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-account-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './account-page.html',
  styleUrl: './account-page.css'
})
export class AccountPage implements OnInit {
  user: ApplicationUser | null = null;
  updateData: UpdateUserDto = {};
  isLoading = true;
  isUpdating = false;
  isLoggingOut = false;
  error: string | null = null;
  successMessage: string | null = null;
  activeTab = 'account';

  constructor(
    private userService: UserService,
    private authService: AuthService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadUserData();
  }

  private loadUserData(): void {
    const decoded = this.authService.getUserInfo();
   
    if (decoded?.userId) {
      this.userService.getUserById(decoded.userId).subscribe({
        next: user => {
          this.user = user;
          this.isLoading = false;
        },
        error: err => {
          console.error('AccountPage: Failed to load user', err);
          this.error = 'Failed to load user data';
          this.isLoading = false;
        }
      });
    } else {
      console.error('AccountPage: No userId in token');
      this.error = 'User not authenticated';
      this.isLoading = false;
    }
  }

  switchTab(tab: string): void {
    this.activeTab = tab;
    this.clearMessages();
  }

  onSubmit(): void {
    if (!this.user || this.isUpdating) return;

    const hasData = Object.values(this.updateData).some(value => 
      value !== undefined && value !== null && value !== ''
    );

    if (!hasData) {
      this.error = 'Please fill in at least one field to update';
      return;
    }

    this.isUpdating = true;
    this.clearMessages();
   
    this.userService.updateUser(this.user.id, this.updateData).subscribe({
      next: () => {
        this.successMessage = 'Profile updated successfully!';
        this.updateData = {};
        this.loadUserData();
        this.isUpdating = false;
      },
      error: err => {
        console.error('Update failed:', err);
        this.error = err.error?.message || 'Update failed. Please try again.';
        this.isUpdating = false;
      }
    });
  }

  onLogout(): void {
    if (this.isLoggingOut) return;

    this.isLoggingOut = true;
    this.clearMessages();

    this.authService.logout().subscribe({
      next: () => {
        console.log('Logout successful');
        this.router.navigate(['/login']).then(() => {
          this.isLoggingOut = false;
        });
      },
      error: (err) => {
        console.error('Logout failed:', err);
        this.authService.logoutLocal();
        this.router.navigate(['/login']).then(() => {
          this.isLoggingOut = false;
        });
      }
    });
  }

  private clearMessages(): void {
    this.error = null;
    this.successMessage = null;
  }
}
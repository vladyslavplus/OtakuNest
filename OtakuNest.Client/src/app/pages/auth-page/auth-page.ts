import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthService } from '../../features/user/services/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-auth-page',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './auth-page.html',
  styleUrl: './auth-page.css'
})
export class AuthPage {
  authForm: FormGroup;
  isLoginMode = true;
  errorMessage = '';
  isLoading = false;
  isSwitching = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.authForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      userName: [''],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });
  }

  toggleMode() {
    this.isSwitching = true;
    this.errorMessage = '';

    setTimeout(() => {
      this.isLoginMode = !this.isLoginMode;
      const userNameControl = this.authForm.get('userName');

      if (this.isLoginMode) {
        userNameControl?.clearValidators();
        userNameControl?.setValue('');
      } else {
        userNameControl?.setValidators([Validators.required, Validators.minLength(2)]);
      }

      userNameControl?.updateValueAndValidity();
      this.isSwitching = false;
    }, 150);
  }

  onSubmit() {
    if (this.authForm.invalid || this.isLoading) return;

    this.isLoading = true;
    this.errorMessage = '';

    const { email, password, userName } = this.authForm.value;

    if (this.isLoginMode) {
      this.authService.login({ email, password }).subscribe({
        next: (res) => {
          localStorage.setItem('accessToken', res.accessToken);
          this.router.navigate(['/']);
        },
        error: (err) => {
          this.handleError(err);
        },
        complete: () => {
          this.isLoading = false;
        }
      });
    } else {
      this.authService.register({ email, password, userName }).subscribe({
        next: (res) => {
          localStorage.setItem('accessToken', res.accessToken);
          this.router.navigate(['/']);
        },
        error: (err) => {
          this.handleError(err);
        },
        complete: () => {
          this.isLoading = false;
        }
      });
    }
  }

  private handleError(err: any) {
    this.isLoading = false;

    if (err.status === 401) {
      this.errorMessage = 'Invalid email or password';
    } else if (err.status === 409) {
      this.errorMessage = 'Email already exists';
    } else if (err.status === 400) {
      this.errorMessage = err.error?.message || 'Invalid input data';
    } else if (err.status === 0) {
      this.errorMessage = 'Unable to connect to server';
    } else {
      this.errorMessage = err.error?.message || (this.isLoginMode ? 'Login failed' : 'Registration failed');
    }
  }

  getFieldError(fieldName: string): string {
    const control = this.authForm.get(fieldName);
    if (control && control.errors && control.touched) {
      if (control.errors['required']) {
        return `${this.getFieldDisplayName(fieldName)} is required`;
      }
      if (control.errors['email']) {
        return 'Please enter a valid email address';
      }
      if (control.errors['minlength']) {
        const requiredLength = control.errors['minlength'].requiredLength;
        return `${this.getFieldDisplayName(fieldName)} must be at least ${requiredLength} characters`;
      }
    }
    return '';
  }

  private getFieldDisplayName(fieldName: string): string {
    const displayNames: { [key: string]: string } = {
      'email': 'Email',
      'password': 'Password',
      'userName': 'Username'
    };
    return displayNames[fieldName] || fieldName;
  }

  hasFieldError(fieldName: string): boolean {
    const control = this.authForm.get(fieldName);
    return !!(control && control.errors && control.touched);
  }
}

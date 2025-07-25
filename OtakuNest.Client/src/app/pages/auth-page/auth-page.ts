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

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.authForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      userName: [''], 
      password: ['', [Validators.required]]
    });
  }

  toggleMode() {
    this.isLoginMode = !this.isLoginMode;

    const userNameControl = this.authForm.get('userName');
    if (this.isLoginMode) {
      userNameControl?.clearValidators();
    } else {
      userNameControl?.setValidators(Validators.required);
    }
    userNameControl?.updateValueAndValidity();
  }

  onSubmit() {
    if (this.authForm.invalid) return;

    const { email, password, userName } = this.authForm.value;

    if (this.isLoginMode) {
      this.authService.login({ email, password }).subscribe({
        next: (res) => {
          localStorage.setItem('accessToken', res.accessToken);
          this.router.navigate(['/']); 
        },
        error: (err) => {
          this.errorMessage = err.error?.message || 'Login failed';
        }
      });
    } else {
      this.authService.register({ email, password, userName }).subscribe({
        next: (res) => {
          localStorage.setItem('accessToken', res.accessToken);
          this.router.navigate(['/']);
        },
        error: (err) => {
          this.errorMessage = err.error?.message || 'Registration failed';
        }
      });
    }
  }
}

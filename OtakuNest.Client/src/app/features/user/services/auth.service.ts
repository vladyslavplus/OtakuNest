import { HttpClient } from '@angular/common/http';
import { Injectable, Injector } from '@angular/core';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { AuthResponse } from '../../../core/auth/AuthResponse.model';
import { DecodedToken } from '../../../core/auth/DecodedToken.model';
import { jwtDecode } from 'jwt-decode';
import { CartService } from '../../cart/services/cart.service';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly baseUrl = 'http://localhost:5000/api/Auth';
  private readonly TOKEN_KEY = 'accessToken';

  private authStatus = new BehaviorSubject<boolean>(this.hasValidToken());
  authStatus$ = this.authStatus.asObservable();

  constructor(private http: HttpClient, private injector: Injector) {}

  private get cartService(): CartService {
    return this.injector.get(CartService);
  }  

  register(data: { userName: string; email: string; password: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/register`, data).pipe(
      tap(res => {
        if (res?.accessToken) {
          this.handleAuth(res.accessToken);
          this.cartService.handleLoginSyncWithBackend(); 
        }
      })
    );
  }

  login(data: { email: string; password: string }): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, data).pipe(
      tap(res => {
        if (res?.accessToken) {
          this.handleAuth(res.accessToken);
          this.cartService.handleLoginSyncWithBackend(); 
        }
      })
    );
  }

  refreshToken(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.baseUrl}/refresh`, {}, { withCredentials: true }).pipe(
      tap(res => {
        if (res?.accessToken) {
          this.handleAuth(res.accessToken);
        } else {
          this.clearAuth();
        }
      })
    );
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, {}, { withCredentials: true }).pipe(
      tap(() => {
        this.clearAuth();
        this.cartService.clearCart();
      })
    );
  }

  private handleAuth(token: string): void {
    if (!token) {
      console.warn('AuthService: Attempting to handle auth with empty token');
      return;
    }

    try {
      localStorage.setItem(this.TOKEN_KEY, token);
      this.authStatus.next(true);
    } catch (error) {
      console.error('AuthService: Failed to save token to localStorage', error);
      this.clearAuth();
    }
  }

  private clearAuth(): void {
    try {
      localStorage.removeItem(this.TOKEN_KEY);
    } catch (error) {
      console.error('AuthService: Failed to remove token from localStorage', error);
    }
    this.authStatus.next(false);
  }

  isAuthenticated(): boolean {
    return this.hasValidToken();
  }

  getAccessToken(): string | null {
    try {
      return localStorage.getItem(this.TOKEN_KEY);
    } catch (error) {
      console.error('AuthService: Failed to get token from localStorage', error);
      return null;
    }
  }

  getUserInfo(): DecodedToken | null {
    const token = this.getAccessToken();
    if (!token) return null;

    try {
      const decoded = jwtDecode<DecodedToken>(token);

      const now = Date.now() / 1000;
      if (decoded.exp <= now) {
        this.clearAuth();
        return null;
      }

      return decoded;
    } catch (error) {
      console.error('AuthService: Failed to decode token', error);
      this.clearAuth();
      return null;
    }
  }

  private hasValidToken(): boolean {
    const token = this.getAccessToken();
    if (!token) return false;

    try {
      const decoded = jwtDecode<DecodedToken>(token);
      const now = Date.now() / 1000;

      if (decoded.exp <= now) {
        this.clearAuth();
        return false;
      }

      return true;
    } catch (error) {
      console.error('AuthService: Invalid token format', error);
      this.clearAuth();
      return false;
    }
  }
}

import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { RateLimitInfo } from '../models/rate-limit-info.model';

@Injectable({
  providedIn: 'root'
})
export class RateLimitService {
  private readonly RATE_LIMIT_MAX = 95; 
  private readonly RATE_LIMIT_WINDOW = 30000;
  private readonly RATE_LIMIT_BLOCK_DURATION = 30000; 
  private readonly RATE_LIMIT_WARNING_THRESHOLD = 80; 
  private readonly RATE_LIMIT_STORAGE_KEY = 'cart_rate_limit_info';

  private rateLimitInfo: RateLimitInfo = {
    requestCount: 0,
    windowStart: Date.now(),
    isBlocked: false,
    blockEndTime: 0
  };

  private isRateLimitedSubject = new BehaviorSubject<boolean>(false);
  private rateLimitWarningSubject = new BehaviorSubject<boolean>(false);
  private rateLimitTimeRemainingSubject = new BehaviorSubject<number>(0);
  private errorMessageSubject = new BehaviorSubject<string | null>(null);

  public isRateLimited$ = this.isRateLimitedSubject.asObservable();
  public rateLimitWarning$ = this.rateLimitWarningSubject.asObservable();
  public rateLimitTimeRemaining$ = this.rateLimitTimeRemainingSubject.asObservable();
  public errorMessage$ = this.errorMessageSubject.asObservable();

  private statusCheckInterval?: any;

  constructor() {
    this.loadRateLimitInfo();
    this.startStatusCheck();
  }

  private loadRateLimitInfo(): void {
    try {
      const stored = localStorage.getItem(this.RATE_LIMIT_STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        this.rateLimitInfo = {
          requestCount: parsed.requestCount || 0,
          windowStart: parsed.windowStart || Date.now(),
          isBlocked: parsed.isBlocked || false,
          blockEndTime: parsed.blockEndTime || 0
        };

        const now = Date.now();
        if (now - this.rateLimitInfo.windowStart >= this.RATE_LIMIT_WINDOW) {
          console.log('Rate limit window expired, resetting');
          this.resetRateLimitInfo();
        } else {
          console.log('Loaded rate limit info:', this.rateLimitInfo);
        }
      }
    } catch (error) {
      console.warn('Failed to load rate limit info:', error);
      this.resetRateLimitInfo();
    }
  }

  private saveRateLimitInfo(): void {
    try {
      localStorage.setItem(this.RATE_LIMIT_STORAGE_KEY, JSON.stringify(this.rateLimitInfo));
    } catch (error) {
      console.warn('Failed to save rate limit info:', error);
    }
  }

  private resetRateLimitInfo(): void {
    console.log('Resetting rate limit info');
    this.rateLimitInfo = {
      requestCount: 0,
      windowStart: Date.now(),
      isBlocked: false,
      blockEndTime: 0
    };
    this.saveRateLimitInfo();
    
    this.rateLimitWarningSubject.next(false);
    this.isRateLimitedSubject.next(false);
    this.rateLimitTimeRemainingSubject.next(0);
    this.errorMessageSubject.next(null);
  }

  private startStatusCheck(): void {
    this.statusCheckInterval = setInterval(() => {
      this.updateRateLimitStatus();
    }, 1000);
    this.updateRateLimitStatus(); 
  }

  private updateRateLimitStatus(): void {
    const now = Date.now();

    if (now - this.rateLimitInfo.windowStart >= this.RATE_LIMIT_WINDOW) {
      console.log('Rate limit window reset. Previous count:', this.rateLimitInfo.requestCount);
      this.rateLimitInfo.requestCount = 0;
      this.rateLimitInfo.windowStart = now;
      this.rateLimitWarningSubject.next(false);
      this.saveRateLimitInfo();
    }

    if (this.rateLimitInfo.isBlocked) {
      if (now < this.rateLimitInfo.blockEndTime) {
        const remaining = Math.ceil((this.rateLimitInfo.blockEndTime - now) / 1000);
        this.isRateLimitedSubject.next(true);
        this.rateLimitTimeRemainingSubject.next(remaining);
        this.errorMessageSubject.next(
          `Too many requests! Please wait ${this.formatTime(remaining)} before trying again.`
        );
      } else {
        console.log('Rate limit block expired');
        this.rateLimitInfo.isBlocked = false;
        this.isRateLimitedSubject.next(false);
        this.rateLimitTimeRemainingSubject.next(0);
        this.errorMessageSubject.next(null);
        this.saveRateLimitInfo();
      }
    }

    const shouldWarn = this.rateLimitInfo.requestCount >= this.RATE_LIMIT_WARNING_THRESHOLD && !this.rateLimitInfo.isBlocked;
    this.rateLimitWarningSubject.next(shouldWarn);
  }

  public canMakeRequest(): boolean {
    const now = Date.now();

    if (now - this.rateLimitInfo.windowStart >= this.RATE_LIMIT_WINDOW) {
      console.log('Window expired in canMakeRequest, resetting');
      this.rateLimitInfo.requestCount = 0;
      this.rateLimitInfo.windowStart = now;
      this.rateLimitWarningSubject.next(false);
      this.saveRateLimitInfo();
    }

    if (this.rateLimitInfo.isBlocked) {
      if (now < this.rateLimitInfo.blockEndTime) {
        console.log('Request blocked - still in block period');
        return false;
      } else {
        console.log('Block period expired, unblocking');
        this.rateLimitInfo.isBlocked = false;
        this.isRateLimitedSubject.next(false);
        this.rateLimitTimeRemainingSubject.next(0);
        this.errorMessageSubject.next(null);
        this.saveRateLimitInfo();
      }
    }

    if (this.rateLimitInfo.requestCount >= this.RATE_LIMIT_MAX) {
      console.log('Rate limit exceeded:', this.rateLimitInfo.requestCount, '>=', this.RATE_LIMIT_MAX);
      this.triggerManualRateLimit();
      return false;
    }

    console.log('Request allowed. Current count:', this.rateLimitInfo.requestCount, '/', this.RATE_LIMIT_MAX);
    return true;
  }

  public incrementRequestCount(): void {
    this.rateLimitInfo.requestCount++;
    console.log('Request count incremented to:', this.rateLimitInfo.requestCount);
    this.saveRateLimitInfo();

    if (this.rateLimitInfo.requestCount >= this.RATE_LIMIT_WARNING_THRESHOLD) {
      this.rateLimitWarningSubject.next(true);
      console.log('Rate limit warning triggered at:', this.rateLimitInfo.requestCount);
    }
  }

  public getRemainingRequests(): number {
    const remaining = Math.max(0, this.RATE_LIMIT_MAX - this.rateLimitInfo.requestCount);
    return remaining;
  }

  public triggerManualRateLimit(): void {
    const now = Date.now();
    console.log('Triggering manual rate limit');
    this.rateLimitInfo.isBlocked = true;
    this.rateLimitInfo.blockEndTime = now + this.RATE_LIMIT_BLOCK_DURATION;
    this.isRateLimitedSubject.next(true);
    this.rateLimitTimeRemainingSubject.next(Math.ceil(this.RATE_LIMIT_BLOCK_DURATION / 1000));
    this.errorMessageSubject.next(
      `Too many requests! Please wait ${this.formatTime(Math.ceil(this.RATE_LIMIT_BLOCK_DURATION / 1000))} before continuing.`
    );
    this.saveRateLimitInfo();
  }

  public formatTime(seconds: number): string {
    if (seconds >= 60) {
      const minutes = Math.floor(seconds / 60);
      const remainingSeconds = seconds % 60;
      return `${minutes}m ${remainingSeconds}s`;
    }
    return `${seconds}s`;
  }

  public clearError(): void {
    this.errorMessageSubject.next(null);
  }

  public clearWarning(): void {
    this.rateLimitWarningSubject.next(false);
  }

  public getCurrentStatus(): {
    isRateLimited: boolean;
    rateLimitWarning: boolean;
    rateLimitTimeRemaining: number;
    errorMessage: string | null;
    remainingRequests: number;
  } {
    return {
      isRateLimited: this.isRateLimitedSubject.value,
      rateLimitWarning: this.rateLimitWarningSubject.value,
      rateLimitTimeRemaining: this.rateLimitTimeRemainingSubject.value,
      errorMessage: this.errorMessageSubject.value,
      remainingRequests: this.getRemainingRequests()
    };
  }

  public destroy(): void {
    if (this.statusCheckInterval) {
      clearInterval(this.statusCheckInterval);
    }
    this.saveRateLimitInfo();
  }
}
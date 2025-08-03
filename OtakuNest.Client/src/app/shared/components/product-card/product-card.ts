import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { Product } from '../../../features/product/models/product.model';
import { CartService } from '../../../features/cart/services/cart.service';
import { RouterModule } from '@angular/router';
import { RateLimitService } from '../../../core/limiting/services/rate-limit.service';

@Component({
  selector: 'app-product-card',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './product-card.html',
  styleUrl: './product-card.css'
})
export class ProductCard implements OnInit, OnDestroy {
  @Input() product!: Product;
  cartQuantity: number = 0;
  isLoading: boolean = false;
  
  isRateLimited: boolean = false;
  rateLimitWarning: boolean = false;
  rateLimitTimeRemaining: number = 0;
  errorMessage: string | null = null;
  
  private quantitySubscription?: Subscription;
  private rateLimitSubscriptions: Subscription[] = [];
  
  private lastActionTime: number = 0;
  private readonly DEBOUNCE_TIME = 100; 

  constructor(
    private cartService: CartService,
    public rateLimitService: RateLimitService 
  ) { }

  ngOnInit() {
    this.subscribeToCartQuantity();
    this.subscribeToRateLimit();
    this.updateRateLimitStatus(); 
  }

  ngOnDestroy() {
    if (this.quantitySubscription) {
      this.quantitySubscription.unsubscribe();
    }
    
    this.rateLimitSubscriptions.forEach(sub => sub.unsubscribe());
  }

  private subscribeToCartQuantity() {
    this.quantitySubscription = this.cartService.getCartItemQuantity(this.product.id).subscribe({
      next: (quantity) => {
        this.cartQuantity = quantity;
        console.log(`Cart quantity updated for ${this.product.name}:`, quantity);
      },
      error: (err) => {
        console.error('Failed to get cart quantity:', err);
      }
    });
  }

  private subscribeToRateLimit() {
    this.rateLimitSubscriptions.push(
      this.rateLimitService.isRateLimited$.subscribe(isRateLimited => {
        this.isRateLimited = isRateLimited;
        console.log(`Rate limit status for ${this.product.name}:`, isRateLimited);
      })
    );

    this.rateLimitSubscriptions.push(
      this.rateLimitService.rateLimitWarning$.subscribe(rateLimitWarning => {
        this.rateLimitWarning = rateLimitWarning;
        console.log(`Rate limit warning for ${this.product.name}:`, rateLimitWarning);
      })
    );

    this.rateLimitSubscriptions.push(
      this.rateLimitService.rateLimitTimeRemaining$.subscribe(timeRemaining => {
        this.rateLimitTimeRemaining = timeRemaining;
      })
    );

    this.rateLimitSubscriptions.push(
      this.rateLimitService.errorMessage$.subscribe(errorMessage => {
        this.errorMessage = errorMessage;
      })
    );
  }

  private updateRateLimitStatus() {
    const status = this.rateLimitService.getCurrentStatus();
    this.isRateLimited = status.isRateLimited;
    this.rateLimitWarning = status.rateLimitWarning;
    this.rateLimitTimeRemaining = status.rateLimitTimeRemaining;
    this.errorMessage = status.errorMessage;
  }

  private canPerformAction(): boolean {
    const now = Date.now();
    
    if (now - this.lastActionTime < this.DEBOUNCE_TIME) {
      console.warn('Action blocked by debounce');
      return false;
    }

    if (this.isLoading || this.isRateLimited) {
      console.warn('Action blocked: loading or rate limited');
      return false;
    }

    if (!this.rateLimitService.canMakeRequest()) {
      console.warn('Action blocked by rate limiter');
      return false;
    }

    return true;
  }

  addToCart() {
    if (!this.canPerformAction()) return;
    
    this.lastActionTime = Date.now();
    this.isLoading = true;
   
    const operation = this.cartQuantity === 0 
      ? this.cartService.addItemToCart(this.product.id, 1)
      : this.cartService.changeItemQuantity(this.product.id, 1);

    operation.subscribe({
      next: () => {
        this.isLoading = false;
        this.rateLimitService.incrementRequestCount();
        console.log(this.cartQuantity === 0 ? 'Product added to cart!' : 'Product quantity increased!');
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Failed to add/update cart:', err);
        
        if (err.status === 429) {
          console.warn('Hit backend rate limit');
        }
      }
    });
  }

  removeFromCart() {
    if (!this.canPerformAction() || this.cartQuantity <= 0) return;
    
    this.lastActionTime = Date.now();
    this.isLoading = true;
   
    this.cartService.changeItemQuantity(this.product.id, -1).subscribe({
      next: () => {
        this.isLoading = false;
        this.rateLimitService.incrementRequestCount();
        console.log('Product quantity decreased!');
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Failed to decrease quantity:', err);
        
        if (err.status === 429) {
          console.warn('Hit backend rate limit');
        }
      }
    });
  }

  removeCompletelyFromCart() {
    if (!this.canPerformAction() || this.cartQuantity === 0) return;
    
    this.lastActionTime = Date.now();
    this.isLoading = true;
    
    this.cartService.removeItemFromCart(this.product.id).subscribe({
      next: () => {
        this.isLoading = false;
        this.rateLimitService.incrementRequestCount();
        console.log('Product completely removed from cart!');
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Failed to remove from cart:', err);
        
        if (err.status === 429) {
          console.warn('Hit backend rate limit');
        }
      }
    });
  }

  canAddToCart(): boolean {
    return this.canPerformAction();
  }

  canRemoveFromCart(): boolean {
    return this.canPerformAction() && this.cartQuantity > 0;
  }

  canRemoveCompletelyFromCart(): boolean {
    return this.canPerformAction() && this.cartQuantity > 0;
  }

  getButtonTitle(): string {
    if (this.isRateLimited) {
      return `Please wait ${this.rateLimitService.formatTime(this.rateLimitTimeRemaining)} before making more changes`;
    }
    if (this.rateLimitWarning) {
      return `Warning: ${this.getRemainingRequests()} requests remaining`;
    }
    
    const now = Date.now();
    if (now - this.lastActionTime < this.DEBOUNCE_TIME) {
      const remaining = Math.ceil((this.DEBOUNCE_TIME - (now - this.lastActionTime)) / 1000);
      return `Please wait ${remaining}s between actions`;
    }
    
    return '';
  }

  getRemainingRequests(): number {
    return this.rateLimitService.getRemainingRequests();
  }

  handleRateLimitError(): void {
    if (this.errorMessage) {
      console.warn('Rate limit error:', this.errorMessage);
    }
  }

  clearRateLimitWarning(): void {
    this.rateLimitService.clearWarning();
  }

  isApproachingRateLimit(): boolean {
    const remaining = this.getRemainingRequests();
    return remaining <= 10 && remaining > 0; 
  }
}
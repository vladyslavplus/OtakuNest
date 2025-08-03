import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ProductService } from '../../features/product/services/product.service';
import { CartService } from '../../features/cart/services/cart.service';
import { Product } from '../../features/product/models/product.model';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil, Subscription } from 'rxjs';
import { RateLimitService } from '../../core/limiting/services/rate-limit.service';
import { HttpErrorResponse } from '@angular/common/http';

@Component({
  selector: 'app-product-detail-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './product-detail-page.html',
  styleUrl: './product-detail-page.css'
})
export class ProductDetailPage implements OnInit, OnDestroy {
  product: Product | null = null;
  isLoading = true;
  error: string | null = null;
  isAddingToCart = false;
  addToCartSuccess = false;
  isToastExiting = false;

  isRateLimited = false;
  rateLimitWarning = false;
  rateLimitTimeRemaining = 0;
  rateLimitErrorMessage: string | null = null;

  private destroy$ = new Subject<void>();
  private toastTimeout: any;
  private rateLimitSubscriptions: Subscription[] = [];

  constructor(
    private route: ActivatedRoute,
    private productService: ProductService,
    private cartService: CartService,
    protected rateLimitService: RateLimitService
  ) {}

  ngOnInit(): void {
    this.subscribeToRateLimit();

    const productId = this.route.snapshot.paramMap.get('id');
    if (!productId) {
      this.error = 'Invalid product ID';
      this.isLoading = false;
      return;
    }

    if (!this.rateLimitService.canMakeRequest()) {
      console.warn('Request blocked by rate limiter');
      this.isLoading = false;
      return;
    }

    this.productService.getProductById(productId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: product => {
          this.product = product;
          this.isLoading = false;
          this.rateLimitService.incrementRequestCount();
        },
        error: (err: HttpErrorResponse) => {
          console.error('Failed to load product', err);
          this.isLoading = false;

          if (err.status === 429) {
            console.warn('Rate limit hit on product load');
          } else {
            this.setRegularError('Failed to load product details.');
          }
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();

    if (this.toastTimeout) {
      clearTimeout(this.toastTimeout);
    }

    this.rateLimitSubscriptions.forEach(sub => sub.unsubscribe());
  }

  private subscribeToRateLimit(): void {
    this.rateLimitSubscriptions.push(
      this.rateLimitService.isRateLimited$.subscribe(isRateLimited => {
        this.isRateLimited = isRateLimited;
        console.log('ProductDetailPage - Rate limit status:', isRateLimited);
      })
    );

    this.rateLimitSubscriptions.push(
      this.rateLimitService.rateLimitWarning$.subscribe(rateLimitWarning => {
        this.rateLimitWarning = rateLimitWarning;
        console.log('ProductDetailPage - Rate limit warning:', rateLimitWarning);
      })
    );

    this.rateLimitSubscriptions.push(
      this.rateLimitService.rateLimitTimeRemaining$.subscribe(timeRemaining => {
        this.rateLimitTimeRemaining = timeRemaining;
      })
    );

    this.rateLimitSubscriptions.push(
      this.rateLimitService.errorMessage$.subscribe(errorMessage => {
        this.rateLimitErrorMessage = errorMessage;
        if (errorMessage) {
          this.error = errorMessage;
        }
      })
    );
  }

  addToCart(): void {
    if (!this.product || this.isAddingToCart) return;

    if (!this.rateLimitService.canMakeRequest()) {
      console.warn('Request blocked by rate limiter');
      return;
    }

    if (!this.product.isAvailable || !this.product.quantity) {
      this.setRegularError('This product is currently out of stock.');
      return;
    }

    this.clearRegularError();
    this.isAddingToCart = true;

    if (this.toastTimeout) clearTimeout(this.toastTimeout);

    this.cartService.addItemToCart(this.product.id, 1)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.isAddingToCart = false;
          this.rateLimitService.incrementRequestCount();
          this.addToCartSuccess = true;
          this.isToastExiting = false;

          this.toastTimeout = setTimeout(() => {
            this.isToastExiting = true;
            setTimeout(() => {
              this.addToCartSuccess = false;
              this.isToastExiting = false;
            }, 600);
          }, 2500);
        },
        error: (error: HttpErrorResponse) => {
          console.error('Error adding to cart:', error);
          this.isAddingToCart = false;

          if (error.status === 429) {
            console.warn('Rate limit hit on add to cart');
          } else {
            this.setRegularError(error.message || 'Failed to add item to cart.');
          }
        }
      });
  }

  private setRegularError(message: string): void {
    if (!this.rateLimitErrorMessage) {
      this.error = message;
    }
  }

  private clearRegularError(): void {
    if (!this.rateLimitErrorMessage) {
      this.error = null;
    }
  }

  clearError(): void {
    if (this.rateLimitErrorMessage) {
      this.rateLimitService.clearError();
    } else {
      this.error = null;
    }
  }

  onImageError(event: any): void {
    event.target.style.display = 'none';
    const placeholder = event.target.nextElementSibling;
    if (placeholder) placeholder.style.display = 'flex';
  }

  canAddToCart(): boolean {
    return !!(this.product?.isAvailable &&
              this.product?.quantity &&
              !this.isAddingToCart &&
              !this.isRateLimited &&
              this.rateLimitService.canMakeRequest());
  }

  getStockMessage(): string {
    if (!this.product) return '';
    if (!this.product.isAvailable || !this.product.quantity) return 'Out of Stock';
    if (this.product.quantity <= 5) return `Only ${this.product.quantity} left in stock`;
    return 'In Stock';
  }

  getStockStatusClass(): string {
    if (!this.product) return '';
    if (!this.product.isAvailable || !this.product.quantity) return 'out-of-stock';
    if (this.product.quantity <= 5) return 'low-stock';
    return 'in-stock';
  }

  getOriginalPrice(): string {
    if (!this.product || !this.product.discount) return '0';
    const original = this.product.price / (1 - this.product.discount / 100);
    return original.toFixed(2);
  }

  getRemainingRequests(): number {
    return this.rateLimitService.getRemainingRequests();
  }

  clearRateLimitWarning(): void {
    this.rateLimitService.clearWarning();
  }

  getCurrentRateLimitStatus(): any {
    return this.rateLimitService.getCurrentStatus();
  }

  public formatTime(seconds: number): string {
    return this.rateLimitService.formatTime(seconds);
  }
}
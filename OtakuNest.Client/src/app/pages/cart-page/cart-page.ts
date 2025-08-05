import { Component, OnInit, OnDestroy } from '@angular/core';
import { CartService } from '../../features/cart/services/cart.service';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { DetailedCartItem } from '../../features/cart/models/detailed-cart-item.model';
import { ProductService } from '../../features/product/services/product.service';
import { CartItemDto } from '../../features/cart/models/cart-item.dto';
import { Subject, forkJoin, takeUntil, catchError, debounceTime, Subscription } from 'rxjs';
import { of } from 'rxjs';
import { AuthService } from '../../features/user/services/auth.service';
import { RateLimitService } from '../../core/limiting/services/rate-limit.service';

@Component({
  selector: 'app-cart-page',
  imports: [CommonModule, RouterModule],
  templateUrl: './cart-page.html',
  styleUrl: './cart-page.css'
})
export class CartPage implements OnInit, OnDestroy {
  detailedCartItems: DetailedCartItem[] = [];
  totalQuantity = 0;
  totalPrice = 0;
  isLoading = false;
  error: string | null = null;

  isRateLimited = false;
  rateLimitWarning = false;
  rateLimitTimeRemaining = 0;
  rateLimitErrorMessage: string | null = null;

  pendingRequests = new Set<string>();
  private destroy$ = new Subject<void>();
  private rateLimitSubscriptions: Subscription[] = [];

  constructor(
    private cartService: CartService,
    private productService: ProductService,
    private router: Router,
    private authService: AuthService,
    private rateLimitService: RateLimitService
  ) {}

  ngOnInit(): void {
    this.subscribeToRateLimit();
    
    this.cartService.cartItems$
      .pipe(
        takeUntil(this.destroy$),
        debounceTime(50) 
      )
      .subscribe(cartItems => {
        this.loadProductDetails(cartItems);
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    
    this.rateLimitSubscriptions.forEach(sub => sub.unsubscribe());
  }

  private subscribeToRateLimit(): void {
    this.rateLimitSubscriptions.push(
      this.rateLimitService.isRateLimited$.subscribe(isRateLimited => {
        this.isRateLimited = isRateLimited;
        console.log('CartPage - Rate limit status:', isRateLimited);
      })
    );

    this.rateLimitSubscriptions.push(
      this.rateLimitService.rateLimitWarning$.subscribe(rateLimitWarning => {
        this.rateLimitWarning = rateLimitWarning;
        console.log('CartPage - Rate limit warning:', rateLimitWarning);
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

  public formatTime(seconds: number): string {
    return this.rateLimitService.formatTime(seconds);
  }

  goToOrderPage(): void {
    if (this.detailedCartItems.length === 0) {
      this.error = 'Your cart is empty';
      return;
    }

    if (!this.authService.isAuthenticated()) {
      this.error = 'Please log in to place an order';
      setTimeout(() => {
        this.router.navigate(['/auth']);
      }, 1500);
      return;
    }

    const unavailableItems = this.detailedCartItems.filter(item => !item.isAvailable);
    if (unavailableItems.length > 0) {
      this.error = `Some items are no longer available: ${unavailableItems.map(item => item.productName).join(', ')}`;
      return;
    }

    const insufficientStockItems = this.detailedCartItems.filter(item => 
      item.quantity > item.availableQuantity
    );
    if (insufficientStockItems.length > 0) {
      this.error = `Insufficient stock for: ${insufficientStockItems.map(item => 
        `${item.productName} (requested: ${item.quantity}, available: ${item.availableQuantity})`
      ).join(', ')}`;
      return;
    }

    this.router.navigate(['/order']);
  }

  private loadProductDetails(cartItems: CartItemDto[]): void {
    if (cartItems.length === 0) {
      this.detailedCartItems = [];
      this.calculateTotals();
      return;
    }

    if (this.detailedCartItems.length === 0) {
      this.isLoading = true;
    }

    this.clearRegularError(); 

    const existingProductIds = new Set(this.detailedCartItems.map(item => item.productId));
    const newCartItems = cartItems.filter(item => !existingProductIds.has(item.productId));

    const cartItemsMap = new Map(cartItems.map(item => [item.productId, item]));

    this.detailedCartItems = this.detailedCartItems
      .filter(detailedItem => cartItemsMap.has(detailedItem.productId))
      .map(detailedItem => {
        const cartItem = cartItemsMap.get(detailedItem.productId);
        return {
          ...detailedItem,
          quantity: cartItem!.quantity
        };
      });

    if (newCartItems.length > 0) {
      const productRequests = newCartItems.map(item =>
        this.productService.getProductById(item.productId).pipe(
          catchError(error => {
            console.error(`Error loading product ${item.productId}:`, error);
            return of(null);
          })
        )
      );

      forkJoin(productRequests)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (products) => {
            const newDetailedItems: DetailedCartItem[] = [];

            newCartItems.forEach((item, index) => {
              const product = products[index];
              if (product) {
                const detailed: DetailedCartItem = {
                  productId: item.productId,
                  quantity: item.quantity,
                  productName: product.name,
                  unitPrice: product.price,
                  imageUrl: product.imageUrl,
                  availableQuantity: product.quantity,
                  isAvailable: product.isAvailable
                };
                newDetailedItems.push(detailed);
              }
            });

            this.detailedCartItems = [...this.detailedCartItems, ...newDetailedItems];
            this.calculateTotals();
            this.isLoading = false;
          },
          error: (error) => {
            console.error('Error loading cart products:', error);
            this.setRegularError('Failed to load cart items. Please try again later.');
            this.isLoading = false;
          }
        });
    } else {
      this.calculateTotals();
      this.isLoading = false;
    }
  }

  private calculateTotals(): void {
    this.totalQuantity = this.detailedCartItems.reduce((sum, item) => sum + item.quantity, 0);
    this.totalPrice = this.detailedCartItems.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0);
  }

  removeItem(productId: string): void {
    if (this.pendingRequests.has(`remove_${productId}`)) {
      return;
    }

    if (!this.rateLimitService.canMakeRequest()) {
      console.warn('Request blocked by rate limiter');
      return;
    }

    this.pendingRequests.add(`remove_${productId}`);

    this.cartService.removeItemFromCart(productId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.pendingRequests.delete(`remove_${productId}`);
          console.log('Item removed from cart!');
        },
        error: (error) => {
          console.error('Error removing item:', error);
          this.setRegularError('Failed to remove item. Please try again later.');
          this.pendingRequests.delete(`remove_${productId}`);
        }
      });
  }

  increaseQuantity(productId: string): void {
    const requestKey = `increase_${productId}`;
    if (this.pendingRequests.has(requestKey)) {
      return;
    }

    if (!this.rateLimitService.canMakeRequest()) {
      console.warn('Request blocked by rate limiter');
      return;
    }

    const item = this.detailedCartItems.find(i => i.productId === productId);

    if (!item) {
      this.setRegularError('Item not found in cart.');
      return;
    }

    if (!item.isAvailable) {
      this.setRegularError(`${item.productName} is currently unavailable.`);
      return;
    }

    if (item.quantity >= item.availableQuantity) {
      this.setRegularError(`Sorry, only ${item.availableQuantity} units of ${item.productName} are available.`);
      return;
    }

    this.pendingRequests.add(requestKey);

    this.cartService.changeItemQuantity(productId, 1)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.pendingRequests.delete(requestKey);
          console.log('Item quantity increased!');
        },
        error: (error) => {
          console.error('Error increasing quantity:', error);
          this.setRegularError('Failed to update quantity. Please try again later.');
          this.pendingRequests.delete(requestKey);
        }
      });
  }

  decreaseQuantity(productId: string): void {
    const requestKey = `decrease_${productId}`;
    if (this.pendingRequests.has(requestKey)) {
      return;
    }

    if (!this.rateLimitService.canMakeRequest()) {
      console.warn('Request blocked by rate limiter');
      return;
    }

    this.pendingRequests.add(requestKey);

    this.cartService.changeItemQuantity(productId, -1)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.pendingRequests.delete(requestKey);
          console.log('Item quantity decreased!');
        },
        error: (error) => {
          console.error('Error decreasing quantity:', error);
          this.setRegularError('Failed to update quantity. Please try again later.');
          this.pendingRequests.delete(requestKey);
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

  trackByProductId(index: number, item: DetailedCartItem): string {
    return item.productId;
  }

  onImageError(event: any): void {
    event.target.style.display = 'none';
    const placeholder = event.target.nextElementSibling;
    if (placeholder) {
      placeholder.style.display = 'flex';
    }
  }

  canIncreaseQuantity(item: DetailedCartItem): boolean {
    const isRequestPending = this.pendingRequests.has(`increase_${item.productId}`);
    return !isRequestPending && 
           !this.isRateLimited && 
           this.rateLimitService.canMakeRequest() && 
           item.isAvailable && 
           item.quantity < item.availableQuantity;
  }

  canDecreaseQuantity(item: DetailedCartItem): boolean {
    const isRequestPending = this.pendingRequests.has(`decrease_${item.productId}`);
    return !isRequestPending && 
           !this.isRateLimited && 
           this.rateLimitService.canMakeRequest() && 
           item.quantity > 1;
  }

  canRemoveItem(productId: string): boolean {
    return !this.pendingRequests.has(`remove_${productId}`) && 
           !this.isRateLimited && 
           this.rateLimitService.canMakeRequest();
  }

  getStockMessage(item: DetailedCartItem): string {
    if (!item.isAvailable) {
      return 'Out of Stock';
    }

    const remaining = item.availableQuantity - item.quantity;
    if (remaining === 0) {
      return 'No more available';
    } else if (remaining <= 5) {
      return `Only ${remaining} left`;
    }

    return 'In Stock';
  }

  getStockStatusClass(item: DetailedCartItem): string {
    if (!item.isAvailable) {
      return 'out-of-stock';
    }

    const remaining = item.availableQuantity - item.quantity;
    if (remaining === 0) {
      return 'no-more-available';
    } else if (remaining <= 5) {
      return 'low-stock';
    }

    return 'in-stock';
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
}
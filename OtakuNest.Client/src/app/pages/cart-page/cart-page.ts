import { Component, OnInit, OnDestroy } from '@angular/core';
import { CartService } from '../../features/cart/services/cart.service';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { DetailedCartItem } from '../../features/cart/models/DetailedCartItem.model';
import { ProductService } from '../../features/product/services/product.service';
import { CartItemDto } from '../../features/cart/models/CartItemDto.model';
import { Subject, forkJoin, takeUntil, catchError, debounceTime } from 'rxjs';
import { of } from 'rxjs';

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

  pendingRequests = new Set<string>();
  private destroy$ = new Subject<void>();

  constructor(
    private cartService: CartService,
    private productService: ProductService
  ) { }

  ngOnInit(): void {
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

    this.error = null;

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
            this.error = 'Failed to load cart items. Please try again later.';
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

    this.pendingRequests.add(`remove_${productId}`);

    this.cartService.removeItemFromCart(productId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.pendingRequests.delete(`remove_${productId}`);
        },
        error: (error) => {
          console.error('Error removing item:', error);
          this.error = 'Failed to remove item. Please try again later.';
          this.pendingRequests.delete(`remove_${productId}`);
        }
      });
  }

  increaseQuantity(productId: string): void {
    const requestKey = `increase_${productId}`;
    if (this.pendingRequests.has(requestKey)) {
      return;
    }

    const item = this.detailedCartItems.find(i => i.productId === productId);

    if (!item) {
      this.error = 'Item not found in cart.';
      return;
    }

    if (!item.isAvailable) {
      this.error = `${item.productName} is currently unavailable.`;
      return;
    }

    if (item.quantity >= item.availableQuantity) {
      this.error = `Sorry, only ${item.availableQuantity} units of ${item.productName} are available.`;
      return;
    }

    this.pendingRequests.add(requestKey);

    this.cartService.changeItemQuantity(productId, 1)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.pendingRequests.delete(requestKey);
        },
        error: (error) => {
          console.error('Error increasing quantity:', error);
          this.error = 'Failed to update quantity. Please try again later.';
          this.pendingRequests.delete(requestKey);
        }
      });
  }

  decreaseQuantity(productId: string): void {
    const requestKey = `decrease_${productId}`;
    if (this.pendingRequests.has(requestKey)) {
      return;
    }

    this.pendingRequests.add(requestKey);

    this.cartService.changeItemQuantity(productId, -1)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.pendingRequests.delete(requestKey);
        },
        error: (error) => {
          console.error('Error decreasing quantity:', error);
          this.error = 'Failed to update quantity. Please try again later.';
          this.pendingRequests.delete(requestKey);
        }
      });
  }

  clearError(): void {
    this.error = null;
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
    return !isRequestPending && item.isAvailable && item.quantity < item.availableQuantity;
  }

  canDecreaseQuantity(item: DetailedCartItem): boolean {
    const isRequestPending = this.pendingRequests.has(`decrease_${item.productId}`);
    return !isRequestPending && item.quantity > 1;
  }

  canRemoveItem(productId: string): boolean {
    return !this.pendingRequests.has(`remove_${productId}`);
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
}
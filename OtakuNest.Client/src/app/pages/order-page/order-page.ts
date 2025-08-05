import { Component, OnInit, OnDestroy } from '@angular/core';
import { DetailedCartItem } from '../../features/cart/models/detailed-cart-item.model';
import { CartService } from '../../features/cart/services/cart.service';
import { OrderService } from '../../features/orders/services/order.service';
import { Router } from '@angular/router';
import { CreateOrderDto } from '../../features/orders/models/create-order.dto';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';
import { OrderStage } from '../../features/orders/models/order-stage.model';
import { OrderDto } from '../../features/orders/models/order.dto';

@Component({
  selector: 'app-order-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './order-page.html',
  styleUrl: './order-page.css'
})
export class OrderPage implements OnInit, OnDestroy {
  cartItems: DetailedCartItem[] = [];
  shippingAddress = '';
  error: string | null = null;
  isSubmitting = false;
  isOrderCompleted = false;
  completedOrder: OrderDto | null = null;

  orderApiCompleted = false;
  animationsCompleted = false;

  orderStages: OrderStage[] = [
    { id: 'validating', label: 'Validating Order', status: 'pending', icon: 'check-circle' },
    { id: 'processing', label: 'Processing Order', status: 'pending', icon: 'shopping-cart' },
    { id: 'completing', label: 'Creating Order', status: 'pending', icon: 'package' }
  ];

  private readonly stageDurations: number[] = [2000, 3000, 4000];

  currentStageIndex = 0;

  autoRedirectCountdown = 5;
  autoRedirectTimer: any = null;

  private destroy$ = new Subject<void>();

  constructor(
    private cartService: CartService,
    private orderService: OrderService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.loadCartItems();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    if (this.autoRedirectTimer) {
      clearInterval(this.autoRedirectTimer);
    }
  }

  private loadCartItems(): void {
    this.cartService.detailedCartItems$
      .pipe(takeUntil(this.destroy$))
      .subscribe(items => {
        this.cartItems = items;

        if (items.length === 0 && !this.isSubmitting && !this.completedOrder) {
          this.router.navigate(['/cart']);
          return;
        }

        this.validateItemsAvailability();
      });
  }

  private validateItemsAvailability(): void {
    const unavailableItems = this.cartItems.filter(item => !item.isAvailable);

    if (unavailableItems.length > 0) {
      const itemNames = unavailableItems.map(item => item.productName).join(', ');
      this.error = `Some items are no longer available: ${itemNames}. Please remove them from your cart before proceeding.`;
    } else if (this.error && this.error.includes('no longer available')) {
      this.error = null;
    }
  }

  submitOrder(): void {
    this.error = null;

    if (!this.shippingAddress.trim()) {
      this.error = 'Please enter a shipping address.';
      this.scrollToError();
      return;
    }

    if (this.shippingAddress.trim().length < 10) {
      this.error = 'Please enter a complete shipping address (minimum 10 characters).';
      this.scrollToError();
      return;
    }

    if (this.cartItems.length === 0) {
      this.error = 'Your cart is empty.';
      this.router.navigate(['/cart']);
      return;
    }

    const unavailableItems = this.cartItems.filter(item => !item.isAvailable);
    if (unavailableItems.length > 0) {
      const itemNames = unavailableItems.map(item => item.productName).join(', ');
      this.error = `Some items are no longer available: ${itemNames}. Please update your cart.`;
      this.scrollToError();
      return;
    }

    const insufficientStockItems = this.cartItems.filter(item =>
      item.quantity > item.availableQuantity
    );

    if (insufficientStockItems.length > 0) {
      const itemDetails = insufficientStockItems.map(item =>
        `${item.productName} (requested: ${item.quantity}, available: ${item.availableQuantity})`
      ).join(', ');
      this.error = `Insufficient stock for: ${itemDetails}`;
      this.scrollToError();
      return;
    }

    this.createOrder();
  }

  private createOrder(): void {
    const createOrderDto: CreateOrderDto = {
      shippingAddress: this.shippingAddress.trim(),
      items: this.cartItems.map(item => ({
        productId: item.productId,
        quantity: item.quantity
      }))
    };

    this.isSubmitting = true;
    this.currentStageIndex = 0;
    this.resetOrderStages();

    this.simulateOrderStages();

    this.orderService.createOrder(createOrderDto)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (order) => {
          console.log('Order created successfully:', order);
          this.completedOrder = order;

          this.cartService.clearCart()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
              next: () => {
                this.orderApiCompleted = true;
                this.checkIfReadyForSuccess();
              },
              error: (clearError) => {
                console.error('Error clearing cart:', clearError);
                this.orderApiCompleted = true;
                this.checkIfReadyForSuccess();
              }
            });
        },
        error: (err) => {
          this.handleOrderError(err);
        }
      });
  }

  private resetOrderStages(): void {
    this.orderStages.forEach(stage => {
      stage.status = 'pending';
    });
    this.currentStageIndex = 0;
    this.orderApiCompleted = false;
    this.animationsCompleted = false;
  }

  private simulateOrderStages(): void {
    let totalDelay = 0;
    let cancelled = false;

    const cancelSimulation = () => cancelled = true;
    this.destroy$.pipe(takeUntil(this.destroy$)).subscribe(() => cancelSimulation());

    this.orderStages.forEach((_, index) => {
      setTimeout(() => {
        if (cancelled) return;

        if (index > 0) this.orderStages[index - 1].status = 'completed';

        this.currentStageIndex = index;
        this.orderStages[index].status = 'active';

        if (index === this.orderStages.length - 1) {
          setTimeout(() => {
            if (cancelled) return;
            this.orderStages[index].status = 'completed';
            this.animationsCompleted = true;
            this.checkIfReadyForSuccess();
          }, this.stageDurations[index]);
        }
      }, totalDelay);

      totalDelay += this.stageDurations[index];
    });
  }

  private checkIfReadyForSuccess(): void {
    if (this.orderApiCompleted && this.animationsCompleted) {
      this.handleOrderSuccess();
    }
  }

  private handleOrderSuccess(): void {
    this.orderStages[2].status = 'completed';
    this.isSubmitting = false;
    this.isOrderCompleted = true;

    setTimeout(() => {
      this.startAutoRedirectTimer();
    }, 1500);
  }

  private startAutoRedirectTimer(): void {
    this.autoRedirectCountdown = 5;

    this.autoRedirectTimer = setInterval(() => {
      this.autoRedirectCountdown--;

      if (this.autoRedirectCountdown <= 0) {
        this.navigateToHome();
      }
    }, 1000);
  }

  private handleOrderError(err: any): void {
    console.error('Error creating order:', err);
    this.isSubmitting = false;
    this.isOrderCompleted = false;
    this.orderApiCompleted = false;
    this.animationsCompleted = false;
    this.resetOrderStages();

    switch (err.status) {
      case 400:
        this.error = err.error?.message || 'Invalid order data. Please check your information.';
        break;
      case 401:
        this.error = 'Authentication failed. Please log in again.';
        setTimeout(() => {
          this.router.navigate(['/auth']);
        }, 3000);
        break;
      case 403:
        this.error = 'You do not have permission to place this order.';
        break;
      case 422:
        this.error = 'Invalid order data. Please check your items and try again.';
        break;
      case 429:
        this.error = 'Too many requests. Please wait a moment and try again.';
        break;
      case 500:
        this.error = 'Server error. Please try again later.';
        break;
      default:
        this.error = err.error?.message || 'Failed to create order. Please try again.';
    }

    this.scrollToError();
  }

  private scrollToError(): void {
    setTimeout(() => {
      const errorElement = document.querySelector('.error-message');
      if (errorElement) {
        errorElement.scrollIntoView({
          behavior: 'smooth',
          block: 'center'
        });
      }
    }, 100);
  }

  navigateToHome(): void {
    if (this.autoRedirectTimer) {
      clearInterval(this.autoRedirectTimer);
      this.autoRedirectTimer = null;
    }

    if (this.completedOrder?.id) {
      this.router.navigate(['/orders', this.completedOrder.id]);
    } else {
      this.router.navigate(['/'], {
        queryParams: { orderSuccess: 'true' }
      });
    }
  }

  goBackToCart(): void {
    if (!this.isSubmitting) {
      this.router.navigate(['/cart']);
    }
  }

  clearError(): void {
    this.error = null;
  }

  getTotalPrice(): number {
    return this.cartItems.reduce((sum, item) =>
      sum + (item.quantity * item.unitPrice), 0
    );
  }

  getTotalQuantity(): number {
    return this.cartItems.reduce((sum, item) =>
      sum + item.quantity, 0
    );
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

  get hasUnavailableItems(): boolean {
    return this.cartItems.some(item => !item.isAvailable);
  }

  get canSubmitOrder(): boolean {
    return !this.isSubmitting &&
      this.shippingAddress.trim().length >= 10 &&
      this.cartItems.length > 0 &&
      !this.hasUnavailableItems;
  }

  getStageIcon(stage: OrderStage): string {
    switch (stage.id) {
      case 'validating':
        return stage.status === 'completed' ? 'check-circle' : 'shield';
      case 'processing':
        return stage.status === 'completed' ? 'check-circle' : 'shopping-cart';
      case 'completing':
        return stage.status === 'completed' ? 'check-circle' : 'package';
      default:
        return 'circle';
    }
  }
}
import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ProductService } from '../../features/product/services/product.service';
import { CartService } from '../../features/cart/services/cart.service';
import { Product } from '../../features/product/models/product.model';
import { CommonModule } from '@angular/common';
import { Subject, takeUntil } from 'rxjs';

@Component({
  selector: 'app-product-detail-page',
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
  private destroy$ = new Subject<void>();
  private toastTimeout: any;

  constructor(
    private route: ActivatedRoute,
    private productService: ProductService,
    private cartService: CartService
  ) {}

  ngOnInit(): void {
    const productId = this.route.snapshot.paramMap.get('id');
    if (productId) {
      this.productService.getProductById(productId).subscribe({
        next: product => {
          this.product = product;
          this.isLoading = false;
        },
        error: err => {
          console.error('Failed to load product', err);
          this.error = 'Failed to load product details.';
          this.isLoading = false;
        }
      });
    } else {
      this.error = 'Invalid product ID';
      this.isLoading = false;
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
    
    if (this.toastTimeout) {
      clearTimeout(this.toastTimeout);
    }
  }

  addToCart(): void {
    if (!this.product || this.isAddingToCart) {
      return;
    }

    if (!this.product.isAvailable || !this.product.quantity || this.product.quantity === 0) {
      this.error = 'This product is currently out of stock.';
      return;
    }

    this.error = null;
    
    if (this.toastTimeout) {
      clearTimeout(this.toastTimeout);
    }

    this.cartService.addItemToCart(this.product.id, 1)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
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
        error: (error) => {
          console.error('Error adding to cart:', error);
          this.error = error.message || 'Failed to add item to cart. Please try again.';
        }
      });
  }

  clearError(): void {
    this.error = null;
  }

  onImageError(event: any): void {
    event.target.style.display = 'none';
    const placeholder = event.target.nextElementSibling;
    if (placeholder) {
      placeholder.style.display = 'flex';
    }
  }

  canAddToCart(): boolean {
    return !!(this.product?.isAvailable &&
           this.product?.quantity &&
           this.product.quantity > 0);
  }

  getStockMessage(): string {
    if (!this.product) return '';
   
    if (!this.product.isAvailable || !this.product.quantity || this.product.quantity === 0) {
      return 'Out of Stock';
    } else if (this.product.quantity <= 5) {
      return `Only ${this.product.quantity} left in stock`;
    }
    return 'In Stock';
  }

  getStockStatusClass(): string {
    if (!this.product) return '';
   
    if (!this.product.isAvailable || !this.product.quantity || this.product.quantity === 0) {
      return 'out-of-stock';
    } else if (this.product.quantity <= 5) {
      return 'low-stock';
    }
    return 'in-stock';
  }

  getOriginalPrice(): string {
    if (!this.product || !this.product.discount) return '0';
    
    const originalPrice = this.product.price / (1 - this.product.discount / 100);
    return originalPrice.toFixed(2);
  }
}
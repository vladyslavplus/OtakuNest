import { CommonModule } from '@angular/common';
import { Component, Input, OnInit } from '@angular/core';
import { Product } from '../../../features/product/models/product.model';
import { CartService } from '../../../features/cart/services/cart.service';

@Component({
  selector: 'app-product-card',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './product-card.html',
  styleUrl: './product-card.css'
})
export class ProductCard implements OnInit {
  @Input() product!: Product;
  cartQuantity: number = 0;
  isLoading: boolean = false;

  constructor(private cartService: CartService) { }

  ngOnInit() {
    this.getCartItemQuantity();
  }

  getCartItemQuantity() {
    this.cartService.getCartItemQuantity(this.product.id).subscribe({
      next: (quantity) => {
        this.cartQuantity = quantity;
      },
      error: (err) => {
        console.error('Failed to get cart quantity:', err);
      }
    });
  }

  addToCart() {
    if (this.isLoading) return;
    
    this.isLoading = true;
    
    if (this.cartQuantity === 0) {
      this.cartService.addItemToCart(this.product.id, 1).subscribe({
        next: () => {
          this.cartQuantity = 1;
          this.isLoading = false;
          console.log('Product added to cart!');
        },
        error: (err) => {
          this.isLoading = false;
          console.error('Failed to add to cart:', err);
        }
      });
    } else {
      this.cartService.changeItemQuantity(this.product.id, 1).subscribe({
        next: () => {
          this.cartQuantity++;
          this.isLoading = false;
          console.log('Product quantity increased!');
        },
        error: (err) => {
          this.isLoading = false;
          console.error('Failed to increase quantity:', err);
        }
      });
    }
  }

  removeFromCart() {
    if (this.isLoading || this.cartQuantity <= 0) return;
    
    this.isLoading = true;
    
    this.cartService.changeItemQuantity(this.product.id, -1).subscribe({
      next: () => {
        this.cartQuantity--;
        this.isLoading = false;
        console.log('Product quantity decreased!');
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Failed to decrease quantity:', err);
      }
    });
  }

  removeCompletelyFromCart() {
    if (this.isLoading) return;
    
    this.isLoading = true;
    this.cartService.removeItemFromCart(this.product.id).subscribe({
      next: () => {
        this.cartQuantity = 0;
        this.isLoading = false;
        console.log('Product completely removed from cart!');
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Failed to remove from cart:', err);
      }
    });
  }
}

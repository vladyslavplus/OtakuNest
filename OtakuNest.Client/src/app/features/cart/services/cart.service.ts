import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { BehaviorSubject, catchError, forkJoin, map, Observable, of, tap, throwError } from 'rxjs';
import { AuthService } from '../../user/services/auth.service';
import { CartDto } from '../models/cart.dto';
import { CartItemDto } from '../models/cart-item.dto';
import { AddCartItemDto } from '../models/add-cart-item.dto';
import { UpdateCartItemQuantityDto } from '../models/update-cart-item-quantity.dto';
import { ProductService } from '../../product/services/product.service';
import { DetailedCartItem } from '../models/detailed-cart-item.model';
import { RateLimitService } from '../../../core/limiting/services/rate-limit.service';

@Injectable({
  providedIn: 'root'
})
export class CartService {
  private readonly baseUrl = 'http://localhost:5000/api/cart';
  private readonly LOCAL_KEY = 'user_cart';

  private cartItemsSubject = new BehaviorSubject<CartItemDto[]>([]);
  cartItems$ = this.cartItemsSubject.asObservable();

  private detailedCartItemsSubject = new BehaviorSubject<DetailedCartItem[]>([]);
  detailedCartItems$ = this.detailedCartItemsSubject.asObservable();

  private orderItems: { productId: string; quantity: number; }[] = [];

  setOrderItems(items: { productId: string; quantity: number; }[]): void {
    this.orderItems = items;
  }
  
  getOrderItems(): { productId: string; quantity: number; }[] {
    return this.orderItems;
  }

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private productService: ProductService,
    private rateLimitService: RateLimitService
  ) {
    this.initializeCart();
  }

  private getHeaders() {
    const token = this.authService.getAccessToken();
    return token
      ? { headers: { Authorization: `Bearer ${token}` } }
      : {};
  }

  private initializeCart() {
    if (this.authService.isAuthenticated()) {
      this.loadCartFromBackend().subscribe();
    } else {
      this.loadCartFromLocalStorage();
    }
  }

  private loadCartFromBackend(): Observable<CartDto> {
    return this.http.get<CartDto>(this.baseUrl, this.getHeaders()).pipe(
      tap(cart => {
        this.cartItemsSubject.next(cart.items);
        this.loadDetailedCartItems(cart.items);
      }),
      catchError(() => {
        this.cartItemsSubject.next([]);
        this.detailedCartItemsSubject.next([]);
        return of({ items: [] } as CartDto);
      })
    );
  }

  private loadCartFromLocalStorage() {
    try {
      const raw = localStorage.getItem(this.LOCAL_KEY);
      if (raw) {
        const parsed: CartItemDto[] = JSON.parse(raw);
        this.cartItemsSubject.next(parsed);
        this.loadDetailedCartItems(parsed);
      }
    } catch (e) {
      console.error('Failed to load local cart', e);
      this.cartItemsSubject.next([]);
      this.detailedCartItemsSubject.next([]);
    }
  }

  private loadDetailedCartItems(cartItems: CartItemDto[]): void {
    if (!cartItems || cartItems.length === 0) {
      this.detailedCartItemsSubject.next([]);
      return;
    }
  
    const requests = cartItems.map(item =>
      this.productService.getProductById(item.productId).pipe(
        map(product => {
          return {
            productId: item.productId,
            quantity: item.quantity,
            productName: product.name,
            unitPrice: product.price,
            imageUrl: product.imageUrl,
            availableQuantity: product.quantity,
            isAvailable: product.isAvailable
          } as DetailedCartItem;
        }),
        catchError(() => of(null))
      )
    );
  
    forkJoin(requests).subscribe(items => {
      const detailedItems = items.filter(Boolean) as DetailedCartItem[];
      this.detailedCartItemsSubject.next(detailedItems);
    });
  }

  private saveToLocalStorage() {
    localStorage.setItem(this.LOCAL_KEY, JSON.stringify(this.cartItemsSubject.value));
  }

  private clearLocalStorage() {
    localStorage.removeItem(this.LOCAL_KEY);
  }

  refreshCart(): void {
    if (this.authService.isAuthenticated()) {
      this.loadCartFromBackend().subscribe();
    } else {
      this.loadCartFromLocalStorage();
    }
  }

  private async checkProductAvailability(productId: string, requestedQuantity: number): Promise<boolean> {
    try {
      const product = await this.productService.getProductById(productId).toPromise();
      if (!product || !product.isAvailable) {
        throw new Error('Product is not available');
      }

      const currentCartItem = this.cartItemsSubject.value.find(item => item.productId === productId);
      const currentQuantityInCart = currentCartItem ? currentCartItem.quantity : 0;
      const totalQuantity = currentQuantityInCart + requestedQuantity;

      if (totalQuantity > product.quantity) {
        throw new Error(`Only ${product.quantity} units available. You already have ${currentQuantityInCart} in cart.`);
      }

      return true;
    } catch (error) {
      throw error;
    }
  }

  private handleHttpError(error: any): Observable<never> {
    console.error('Cart service error:', error);

    if (error.status === 429) {
      this.rateLimitService.incrementRequestCount();
      const retryAfter = error.error?.retryAfter || 60;
      return throwError(() => ({
        ...error,
        isRateLimit: true,
        retryAfter: retryAfter,
        message: error.error?.message || 'Rate limit exceeded. Please try again later.'
      }));
    } else if (error.status === 0 || error.statusText === 'Unknown Error') {
      console.warn('Possible CORS-blocked rate limit error detected in cart service');
      this.rateLimitService.incrementRequestCount();
      
      const remainingRequests = this.rateLimitService.getRemainingRequests();
      if (remainingRequests <= 0) {
        return throwError(() => ({
          ...error,
          isRateLimit: true,
          message: 'Too many requests. Please wait before making more changes.'
        }));
      } else {
        return throwError(() => ({
          ...error,
          message: 'Network error occurred. You may be making requests too quickly.'
        }));
      }
    } else if (error.status === 400 && error.error?.message) {
      return throwError(() => new Error(error.error.message));
    }

    return throwError(() => error);
  }

  addItemToCart(productId: string, quantity: number = 1): Observable<any> {
    const dto: AddCartItemDto = { productId, quantity };

    if (this.authService.isAuthenticated()) {
      if (!this.rateLimitService.canMakeRequest()) {
        return throwError(() => new Error('Rate limit exceeded. Please wait before making more requests.'));
      }

      return this.http.post(this.baseUrl, dto, this.getHeaders()).pipe(
        tap(() => {
          this.rateLimitService.incrementRequestCount();
          this.refreshCart();
        }),
        catchError(error => this.handleHttpError(error))
      );
    } else {
      return new Observable(observer => {
        this.checkProductAvailability(productId, quantity)
          .then(() => {
            const current = [...this.cartItemsSubject.value];
            const existing = current.find(i => i.productId === productId);
            if (existing) {
              existing.quantity += quantity;
            } else {
              current.push({ productId, quantity });
            }
            this.cartItemsSubject.next(current);
            this.saveToLocalStorage();
            this.loadDetailedCartItems(current);
            observer.next(null);
            observer.complete();
          })
          .catch(error => {
            observer.error(error);
          });
      });
    }
  }

  removeItemFromCart(productId: string): Observable<any> {
    if (this.authService.isAuthenticated()) {
      if (!this.rateLimitService.canMakeRequest()) {
        return throwError(() => new Error('Rate limit exceeded. Please wait before making more requests.'));
      }

      return this.http.delete(`${this.baseUrl}/${productId}`, this.getHeaders()).pipe(
        tap(() => {
          this.rateLimitService.incrementRequestCount();
          this.refreshCart();
        }),
        catchError(error => this.handleHttpError(error))
      );
    } else {
      const updated = this.cartItemsSubject.value.filter(i => i.productId !== productId);
      this.cartItemsSubject.next(updated);
      this.saveToLocalStorage();
      this.loadDetailedCartItems(updated);
      return of(null);
    }
  }

  changeItemQuantity(productId: string, delta: number): Observable<any> {
    const dto: UpdateCartItemQuantityDto = { productId, delta };

    if (this.authService.isAuthenticated()) {
      if (!this.rateLimitService.canMakeRequest()) {
        return throwError(() => new Error('Rate limit exceeded. Please wait before making more requests.'));
      }

      return this.http.patch(`${this.baseUrl}/quantity`, dto, this.getHeaders()).pipe(
        tap(() => {
          this.rateLimitService.incrementRequestCount();
          this.refreshCart();
        }),
        catchError(error => this.handleHttpError(error))
      );
    } else {
      if (delta > 0) {
        return new Observable(observer => {
          this.checkProductAvailability(productId, delta)
            .then(() => {
              const current = [...this.cartItemsSubject.value];
              const index = current.findIndex(i => i.productId === productId);
              if (index >= 0) {
                current[index].quantity += delta;
                this.cartItemsSubject.next(current);
                this.saveToLocalStorage();
                this.loadDetailedCartItems(current);
              }
              observer.next(null);
              observer.complete();
            })
            .catch(error => {
              observer.error(error);
            });
        });
      } else {
        const current = [...this.cartItemsSubject.value];
        const index = current.findIndex(i => i.productId === productId);
        if (index >= 0) {
          current[index].quantity += delta;
          if (current[index].quantity <= 0) {
            current.splice(index, 1);
          }
          this.cartItemsSubject.next(current);
          this.saveToLocalStorage();
          this.loadDetailedCartItems(current);
        }
        return of(null);
      }
    }
  }

  clearCart(): Observable<any> {
    if (this.authService.isAuthenticated()) {
      if (!this.rateLimitService.canMakeRequest()) {
        return throwError(() => new Error('Rate limit exceeded. Please wait before making more requests.'));
      }

      return this.http.delete(`${this.baseUrl}/clear`, this.getHeaders()).pipe(
        tap(() => {
          this.rateLimitService.incrementRequestCount();
          this.cartItemsSubject.next([]);
          this.detailedCartItemsSubject.next([]);
        }),
        catchError(error => this.handleHttpError(error))
      );
    } else {
      this.cartItemsSubject.next([]);
      this.detailedCartItemsSubject.next([]);
      this.clearLocalStorage();
      return of(null);
    }
  }

  getCartItemQuantity(productId: string): Observable<number> {
    return this.cartItems$.pipe(
      map(items => items.find(i => i.productId === productId)?.quantity ?? 0)
    );
  }

  handleLoginSyncWithBackend(): void {
    const localItems = [...this.cartItemsSubject.value];

    if (localItems.length === 0) {
      this.refreshCart();
      return;
    }

    if (!this.rateLimitService.canMakeRequest()) {
      console.warn('Cannot sync cart due to rate limit');
      return;
    }

    const requests = localItems.map(item =>
      this.http.post(this.baseUrl, {
        productId: item.productId,
        quantity: item.quantity
      } as AddCartItemDto, this.getHeaders()).pipe(
        catchError(error => {
          console.error('Error syncing cart item:', error);
          return of(null);
        })
      )
    );

    forkJoin(requests).subscribe({
      next: () => {
        this.rateLimitService.incrementRequestCount();
        this.clearLocalStorage();
        this.refreshCart();
      },
      error: (error) => {
        console.error('Error during cart sync:', error);
      }
    });
  }

  canMakeRequest(): boolean {
    return this.rateLimitService.canMakeRequest();
  }

  getRemainingRequests(): number {
    return this.rateLimitService.getRemainingRequests();
  }
}
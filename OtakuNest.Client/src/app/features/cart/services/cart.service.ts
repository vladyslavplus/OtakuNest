import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { BehaviorSubject, catchError, map, Observable, of, tap } from 'rxjs';
import { AuthService } from '../../user/services/auth.service';
import { CartDto } from '../models/CartDto.model';
import { CartItemDto } from '../models/CartItemDto.model';
import { AddCartItemDto } from '../models/AddCartItemDto.model';
import { UpdateCartItemQuantityDto } from '../models/UpdateCartItemQuantityDto.model';

@Injectable({
  providedIn: 'root'
})
export class CartService {
  private readonly baseUrl = 'http://localhost:5000/api/cart';
  private readonly LOCAL_KEY = 'user_cart';

  private cartItemsSubject = new BehaviorSubject<CartItemDto[]>([]);
  cartItems$ = this.cartItemsSubject.asObservable();

  constructor(private http: HttpClient, private authService: AuthService) {
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
      tap(cart => this.cartItemsSubject.next(cart.items)),
      catchError(() => {
        this.cartItemsSubject.next([]);
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
      }
    } catch (e) {
      console.error('Failed to load local cart', e);
      this.cartItemsSubject.next([]);
    }
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

  addItemToCart(productId: string, quantity: number = 1): Observable<any> {
    const dto: AddCartItemDto = { productId, quantity };

    if (this.authService.isAuthenticated()) {
      return this.http.post(this.baseUrl, dto, this.getHeaders()).pipe(
        tap(() => this.refreshCart())
      );
    } else {
      const current = [...this.cartItemsSubject.value];
      const existing = current.find(i => i.productId === productId);
      if (existing) {
        existing.quantity += quantity;
      } else {
        current.push({ productId, quantity });
      }
      this.cartItemsSubject.next(current);
      this.saveToLocalStorage();
      return of(null);
    }
  }

  removeItemFromCart(productId: string): Observable<any> {
    if (this.authService.isAuthenticated()) {
      return this.http.delete(`${this.baseUrl}/${productId}`, this.getHeaders()).pipe(
        tap(() => this.refreshCart())
      );
    } else {
      const updated = this.cartItemsSubject.value.filter(i => i.productId !== productId);
      this.cartItemsSubject.next(updated);
      this.saveToLocalStorage();
      return of(null);
    }
  }

  changeItemQuantity(productId: string, delta: number): Observable<any> {
    const dto: UpdateCartItemQuantityDto = { productId, delta };

    if (this.authService.isAuthenticated()) {
      return this.http.patch(`${this.baseUrl}/quantity`, dto, this.getHeaders()).pipe(
        tap(() => this.refreshCart())
      );
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
      }
      return of(null);
    }
  }

  clearCart(): Observable<any> {
    if (this.authService.isAuthenticated()) {
      return this.http.delete(`${this.baseUrl}/clear`, this.getHeaders()).pipe(
        tap(() => this.cartItemsSubject.next([]))
      );
    } else {
      this.cartItemsSubject.next([]);
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

    const requests = localItems.map(item =>
      this.http.post(this.baseUrl, {
        productId: item.productId,
        quantity: item.quantity
      } as AddCartItemDto, this.getHeaders())
    );

    Promise.all(requests.map(r => r.toPromise()))
      .then(() => {
        this.clearLocalStorage();
        this.refreshCart();
      })
      .catch(() => {
      });
  }
}

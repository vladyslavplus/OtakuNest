import { Component, OnInit, OnDestroy } from '@angular/core';
import { ApplicationUser } from '../../features/user/models/application-user.model';
import { UpdateUserDto } from '../../features/user/models/update-user.dto';
import { UserService } from '../../features/user/services/user.service';
import { AuthService } from '../../features/user/services/auth.service';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { OrderDto } from '../../features/orders/models/order.dto';
import { OrderService } from '../../features/orders/services/order.service';
import { ProductService } from '../../features/product/services/product.service';
import { Subject, forkJoin, of } from 'rxjs';
import { takeUntil, catchError, switchMap, map } from 'rxjs/operators';
import { PaginatedResult } from '../../core/pagination/paginated-result.model';
import { OrderParameters } from '../../core/params/order-parameters';

@Component({
  selector: 'app-account-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './account-page.html',
  styleUrls: ['./account-page.css']
})
export class AccountPage implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  user: ApplicationUser | null = null;
  updateData: UpdateUserDto = {};
  isLoading = true;
  isUpdating = false;
  isLoggingOut = false;
  error: string | null = null;
  successMessage: string | null = null;
  activeTab = 'account';

  pagination: PaginatedResult<OrderDto[]>['pagination'] | null = null;
  orderParams: OrderParameters = new OrderParameters();
  orders: OrderDto[] = [];
  Math = Math;
  currentPage = 1;
  ordersPerPage = 6;
  totalPages = 0;

  constructor(
    private userService: UserService,
    private productService: ProductService,
    private authService: AuthService,
    private orderService: OrderService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadUserData();
    this.loadUserOrders();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  trackByOrderId(index: number, order: OrderDto): string {
    return order.id;
  }

  private loadUserData(): void {
    const decoded = this.authService.getUserInfo();

    if (decoded?.userId) {
      this.userService
        .getUserById(decoded.userId)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (user) => {
            this.user = user;
            this.isLoading = false;
          },
          error: (err) => {
            console.error('AccountPage: Failed to load user', err);
            this.error = 'Failed to load user data';
            this.isLoading = false;
          }
        });
    } else {
      console.error('AccountPage: No userId in token');
      this.error = 'User not authenticated';
      this.isLoading = false;
    }
  }

  private loadUserOrders(): void {
    this.orderService
      .getUserOrders(this.orderParams)
      .pipe(
        takeUntil(this.destroy$),
        switchMap((result) => {
          console.log('orderParams:', this.orderParams);
          console.log('Orders result from backend:', result);
          this.orders = result.data;
          this.pagination = result.pagination;
  
          const allProductIds = Array.from(
            new Set(result.data.flatMap(o => o.items.map(i => i.productId)))
          );
  
          const productRequests = allProductIds.map((id) =>
            this.productService.getProductById(id).pipe(
              catchError((err) => {
                console.error(`Failed to load product with ID ${id}`, err);
                return of({ id, name: 'Unknown Product' });
              })
            )
          );
  
          return forkJoin(productRequests).pipe(
            map((products) => {
              const productMap = new Map(products.map(p => [p.id, p.name]));
  
              for (const order of this.orders) {
                for (const item of order.items) {
                  item.productName = productMap.get(item.productId) ?? 'Unknown Product';
                }
              }
            })
          );
        })
      )
      .subscribe({
        error: (err) => {
          console.error('Failed to load orders', err);
        }
      });
  }  

  get paginatedOrders(): OrderDto[] {
    const startIndex = (this.currentPage - 1) * this.ordersPerPage;
    const endIndex = startIndex + this.ordersPerPage;
    return this.orders.slice(startIndex, endIndex);
  }

  get pageNumbers(): number[] {
    if (!this.pagination) return [];
    return Array.from({ length: this.pagination.totalPages }, (_, i) => i + 1);
  }  

  changePage(page: number): void {
    if (!this.pagination) return;
  
    if (page >= 1 && page <= this.pagination.totalPages) {
      this.orderParams.pageNumber = page;
      this.loadUserOrders();
      this.scrollToTop();
    }
  }
  
  previousPage(): void {
    if (this.pagination && this.pagination.hasPrevious) {
      this.orderParams.pageNumber--;
      this.loadUserOrders();
      this.scrollToTop();
    }
  }
  
  nextPage(): void {
    if (this.pagination && this.pagination.hasNext) {
      this.orderParams.pageNumber++;
      this.loadUserOrders();
      this.scrollToTop();
    }
  }  

  switchTab(tab: string): void {
    this.activeTab = tab;
    this.clearMessages();

    if (tab === 'history') {
      this.currentPage = 1;
    }
  }

  onSubmit(): void {
    if (!this.user || this.isUpdating) return;

    const hasData = Object.values(this.updateData).some(
      (value) => value !== undefined && value !== null && value !== ''
    );

    if (!hasData) {
      this.error = 'Please fill in at least one field to update';
      return;
    }

    this.isUpdating = true;
    this.clearMessages();

    this.userService
      .updateUser(this.user.id, this.updateData)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.successMessage = 'Profile updated successfully!';
          this.updateData = {};
          this.loadUserData();
          this.isUpdating = false;
        },
        error: (err) => {
          console.error('Update failed:', err);
          this.error = err.error?.message || 'Update failed. Please try again.';
          this.isUpdating = false;
        }
      });
  }

  onLogout(): void {
    if (this.isLoggingOut) return;

    this.isLoggingOut = true;
    this.clearMessages();

    this.authService
      .logout()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          console.log('Logout successful');
          this.router.navigate(['/login']).then(() => {
            this.isLoggingOut = false;
          });
        },
        error: (err) => {
          console.error('Logout failed:', err);
          this.authService.logoutLocal();
          this.router.navigate(['/login']).then(() => {
            this.isLoggingOut = false;
          });
        }
      });
  }

  private clearMessages(): void {
    this.error = null;
    this.successMessage = null;
  }

  scrollToTop(): void {
    setTimeout(() => {
      window.scrollTo({
        top: 0,
        behavior: 'smooth'
      });
    }, 25);
  }
}

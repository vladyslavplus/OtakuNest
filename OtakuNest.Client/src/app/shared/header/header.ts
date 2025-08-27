import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { catchError, debounceTime, distinctUntilChanged, map, Observable, of, startWith, Subject, switchMap, takeUntil } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../features/user/services/auth.service';
import { CartService } from '../../features/cart/services/cart.service';
import { ProductElasticDto } from '../../features/search/models/product-elastic.dto';
import { SearchService } from '../../features/search/services/search.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterModule, CommonModule, FormsModule],
  templateUrl: './header.html',
  styleUrls: ['./header.css']
})
export class Header implements OnInit, OnDestroy {
  isMobileMenuOpen = false;
  isMobileSearchOpen = false;
  isMobile = false;
  isAuthenticated = false;

  // Desktop search
  searchQuery = '';
  searchResults: ProductElasticDto[] = [];
  showSearchDropdown = false;
  showNoResults = false;
  isSearching = false;

  // Mobile search
  mobileSearchQuery = '';
  mobileSearchResults: ProductElasticDto[] = [];
  showMobileSearchDropdown = false;
  showMobileNoResults = false;
  isMobileSearching = false;

  private readonly searchSubject = new Subject<string>();
  private readonly mobileSearchSubject = new Subject<string>();
  private readonly destroy$ = new Subject<void>();

  private readonly SEARCH_MIN_LENGTH = 2;
  private readonly SEARCH_DEBOUNCE_TIME = 500;
  private readonly SEARCH_RESULTS_LIMIT = 3;
  private readonly MOBILE_BREAKPOINT = 640;
  private readonly SEARCH_BLUR_DELAY = 200;

  cartItemCount$!: Observable<number>;

  constructor(
    private readonly router: Router,
    private readonly searchService: SearchService,
    private readonly authService: AuthService,
    private readonly cartService: CartService
  ) { }

  ngOnInit(): void {
    this.checkScreenSize();
    this.initializeAuthSubscription();
    this.initializeSearchSubscriptions();

    this.cartItemCount$ = this.cartService.cartItems$.pipe(
      map(items => items.reduce((total, item) => total + item.quantity, 0)),
      startWith(0)
    );
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  @HostListener('window:resize')
  onResize(): void {
    this.checkScreenSize();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;

    if (!target.closest('.search-container')) {
      this.hideSearchDropdown();
    }

    if (!target.closest('.mobile-search-wrapper')) {
      this.hideMobileSearchDropdown();
    }
  }

  @HostListener('document:keydown.escape')
  onEsc(): void { 
    this.hideSearchDropdown(); 
    this.hideMobileSearchDropdown(); 
  }

  private initializeAuthSubscription(): void {
    this.authService.authStatus$
      .pipe(takeUntil(this.destroy$))
      .subscribe(status => {
        this.isAuthenticated = status;
      });
  }

  private initializeSearchSubscriptions(): void {
    this.searchSubject.pipe(
      debounceTime(this.SEARCH_DEBOUNCE_TIME),
      distinctUntilChanged((a, b) => a.trim().toLowerCase() === b.trim().toLowerCase()),
      switchMap(query => this.fetchProducts(query, false)),
      takeUntil(this.destroy$)
    ).subscribe(results => {
      this.handleSearchResults(results, false);
    });

    this.mobileSearchSubject.pipe(
      debounceTime(this.SEARCH_DEBOUNCE_TIME),
      distinctUntilChanged(),
      switchMap(query => this.fetchProducts(query, true)),
      takeUntil(this.destroy$)
    ).subscribe(results => {
      this.handleSearchResults(results, true);
    });
  }

  navigateToCategory(category: string): void {
    this.router.navigate(['/products'], {
      queryParams: { category }
    });
    this.closeMobileMenu();
  }

  private handleSearchResults(results: ProductElasticDto[], isMobile: boolean): void {
    if (isMobile) {
      this.isMobileSearching = false;
      this.mobileSearchResults = results;
      this.showMobileNoResults = this.mobileSearchQuery.length >= this.SEARCH_MIN_LENGTH && results.length === 0;
      this.showMobileSearchDropdown = this.mobileSearchQuery.length >= this.SEARCH_MIN_LENGTH;
    } else {
      this.isSearching = false;
      this.searchResults = results;
      this.showNoResults = this.searchQuery.length >= this.SEARCH_MIN_LENGTH && results.length === 0;
      this.showSearchDropdown = this.searchQuery.length >= this.SEARCH_MIN_LENGTH;
    }
  }

  private checkScreenSize(): void {
    this.isMobile = window.innerWidth <= this.MOBILE_BREAKPOINT;

    if (!this.isMobile) {
      this.closeMobileMenu();
    }
  }

  private fetchProducts(query: string, isMobile: boolean): Observable<ProductElasticDto[]> {
    if (!query || query.length < this.SEARCH_MIN_LENGTH) {
      return of([] as ProductElasticDto[]);
    }

    if (isMobile) this.isMobileSearching = true;
    else this.isSearching = true;

    return this.searchService.searchProducts(query, 1, this.SEARCH_RESULTS_LIMIT).pipe(
      catchError((error) => {
        console.error('Search error:', error);
        return of([] as ProductElasticDto[]);
      })
    );
  }

  handleAccountClick(): void {
    const route = this.isAuthenticated ? '/account' : '/auth';
    this.router.navigate([route]);
    this.closeMobileMenu();
  }

  handleLogoClick(event: Event): void {
    event.preventDefault();

    if (this.router.url === '/') {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      this.router.navigate(['/']);
    }

    this.closeMobileMenu();
  }

  toggleCart(): void {
    this.router.navigate(['/cart']);
  }

  toggleMobileMenu(): void {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;

    if (this.isMobileMenuOpen) {
      this.isMobileSearchOpen = false;
    }
  }

  toggleMobileSearch(): void {
    this.isMobileSearchOpen = !this.isMobileSearchOpen;

    if (this.isMobileSearchOpen) {
      this.isMobileMenuOpen = false;
    }
  }

  closeMobileMenu(): void {
    this.isMobileMenuOpen = false;
    this.isMobileSearchOpen = false;
  }

  onMobileMenuClick(event: Event): void {
    event.stopPropagation();
  }

  onSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.searchQuery = input.value.trim();

    if (this.searchQuery.length < this.SEARCH_MIN_LENGTH) {
      this.hideSearchDropdown();
      return;
    }

    this.searchSubject.next(this.searchQuery);
  }

  onSearchFocus(): void {
    if (this.searchQuery.length >= this.SEARCH_MIN_LENGTH) {
      this.showSearchDropdown = true;
    }
  }

  onSearchBlur(): void {
    setTimeout(() => this.hideSearchDropdown(), this.SEARCH_BLUR_DELAY);
  }

  performSearch(): void {
    const query = this.searchQuery.trim();

    if (query) {
      if (this.searchResults.length === 1) {
        this.selectProduct(this.searchResults[0]);
      } else {
        this.navigateToProductsWithSearch(query);
        this.hideSearchDropdown();
      }
    }
  }

  selectProduct(product: ProductElasticDto, event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    
    this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => {
      this.router.navigate(['/products', product.id]);
    });
    
    this.hideSearchDropdown();
    this.searchQuery = '';
  }

  private hideSearchDropdown(): void {
    this.showSearchDropdown = false;
    this.searchResults = [];
    this.showNoResults = false;
    this.isSearching = false;
  }

  onMobileSearchInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.mobileSearchQuery = input.value.trim();

    if (this.mobileSearchQuery.length < this.SEARCH_MIN_LENGTH) {
      this.hideMobileSearchDropdown();
      return;
    }

    this.mobileSearchSubject.next(this.mobileSearchQuery);
  }

  performMobileSearch(): void {
    const query = this.mobileSearchQuery.trim();

    if (query) {
      if (this.mobileSearchResults.length === 1) {
        this.selectMobileProduct(this.mobileSearchResults[0]);
      } else {
        this.navigateToProductsWithSearch(query);
        this.closeMobileMenu();
        this.mobileSearchQuery = '';
      }
    }
  }

  selectMobileProduct(product: ProductElasticDto, event?: Event): void {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    
    this.router.navigateByUrl('/', { skipLocationChange: true }).then(() => {
      this.router.navigate(['/products', product.id]);
    });
    
    this.closeMobileMenu();
    this.mobileSearchQuery = '';
  }

  private hideMobileSearchDropdown(): void {
    this.showMobileSearchDropdown = false;
    this.mobileSearchResults = [];
    this.showMobileNoResults = false;
    this.isMobileSearching = false;
  }

  private navigateToProductsWithSearch(query: string): void {
    this.router.navigate(['/products'], {
      queryParams: { search: query }
    });
  }
}
import { Component, HostListener, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { Product } from '../../features/product/models/product.model';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap, takeUntil } from 'rxjs';
import { ProductService } from '../../features/product/services/product.service';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../features/user/services/auth.service';

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
  searchResults: Product[] = [];
  showSearchDropdown = false;
  showNoResults = false;
  isSearching = false;

  // Mobile search
  mobileSearchQuery = '';
  mobileSearchResults: Product[] = [];
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

  constructor(
    private readonly router: Router,
    private readonly productService: ProductService,
    private readonly authService: AuthService
  ) {}

  ngOnInit(): void {
    this.checkScreenSize();
    this.initializeAuthSubscription();
    this.initializeSearchSubscriptions();
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
      distinctUntilChanged(),
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

  private handleSearchResults(results: Product[], isMobile: boolean): void {
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

  private fetchProducts(query: string, isMobile: boolean) {
    if (!query || query.length < this.SEARCH_MIN_LENGTH) {
      return of([] as Product[]);
    }

    if (isMobile) {
      this.isMobileSearching = true;
    } else {
      this.isSearching = true;
    }

    return this.productService.getProducts({
      name: query,
      pageSize: this.SEARCH_RESULTS_LIMIT,
      pageNumber: 1
    }).pipe(
      catchError((error) => {
        console.error('Search error:', error);
        return of({ data: [], pagination: null });
      }),
      switchMap(response => of(response.data || []))
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
    // TODO: Implement cart functionality
    console.log('Cart toggled');
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
      this.navigateToProductsWithSearch(query);
      this.hideSearchDropdown();
    }
  }

  selectProduct(product: Product): void {
    this.router.navigate(['/products', product.id]);
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
      this.navigateToProductsWithSearch(query);
      this.closeMobileMenu();
      this.mobileSearchQuery = '';
    }
  }

  selectMobileProduct(product: Product): void {
    this.router.navigate(['/products', product.id]);
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
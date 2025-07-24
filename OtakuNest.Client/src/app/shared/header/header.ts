import { Component, HostListener } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { Product } from '../../features/product/models/product.model';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap } from 'rxjs';
import { ProductService } from '../../features/product/services/product.service';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-header',
  imports: [RouterModule, CommonModule, FormsModule],
  templateUrl: './header.html',
  styleUrl: './header.css'
})
export class Header {
  isMobileMenuOpen = false;
  isMobileSearchOpen = false;
  isMobile = false;

  searchQuery = '';
  searchResults: Product[] = [];
  showSearchDropdown = false;
  showNoResults = false;
  isSearching = false;
  private searchSubject = new Subject<string>();
  
  mobileSearchQuery = '';
  mobileSearchResults: Product[] = [];
  showMobileSearchDropdown = false;
  showMobileNoResults = false;
  isMobileSearching = false;
  private mobileSearchSubject = new Subject<string>();

  constructor(
    private router: Router,
    private productService: ProductService
  ) {
    this.checkScreenSize();
    this.setupSearch();
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.checkScreenSize();
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event) {
    const target = event.target as HTMLElement;
    const searchContainer = target.closest('.search-container');
    const mobileSearchWrapper = target.closest('.mobile-search-wrapper');
    
    if (!searchContainer) {
      this.hideSearchDropdown();
    }
    
    if (!mobileSearchWrapper) {
      this.hideMobileSearchDropdown();
    }
  }

  private checkScreenSize() {
    this.isMobile = window.innerWidth <= 640;
    if (!this.isMobile) {
      this.isMobileMenuOpen = false;
      this.isMobileSearchOpen = false;
    }
  }

  private setupSearch() {
    this.searchSubject.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(query => {
        if (!query || query.length < 2) {
          return of([]);
        }
        
        this.isSearching = true;
        return this.productService.getProducts({
          name: query,
          pageSize: 3,
          pageNumber: 1
        }).pipe(
          catchError(() => of({ data: [], pagination: null }))
        );
      })
    ).subscribe(result => {
      this.isSearching = false;
      if (Array.isArray(result)) {
        this.searchResults = result;
      } else {
        this.searchResults = result.data || [];
      }
      
      this.showNoResults = this.searchQuery.length >= 2 && this.searchResults.length === 0;
      this.showSearchDropdown = this.searchQuery.length >= 2;
    });

    this.mobileSearchSubject.pipe(
      debounceTime(500),
      distinctUntilChanged(),
      switchMap(query => {
        if (!query || query.length < 2) {
          return of([]);
        }
        
        this.isMobileSearching = true;
        return this.productService.getProducts({
          name: query,
          pageSize: 3,
          pageNumber: 1
        }).pipe(
          catchError(() => of({ data: [], pagination: null }))
        );
      })
    ).subscribe(result => {
      this.isMobileSearching = false;
      if (Array.isArray(result)) {
        this.mobileSearchResults = result;
      } else {
        this.mobileSearchResults = result.data || [];
      }
      
      this.showMobileNoResults = this.mobileSearchQuery.length >= 2 && this.mobileSearchResults.length === 0;
      this.showMobileSearchDropdown = this.mobileSearchQuery.length >= 2;
    });
  }

  handleLogoClick(event: Event) {
    event.preventDefault();
    const isHome = this.router.url === '/';
    if (isHome) {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    } else {
      this.router.navigate(['/']);
    }
    this.closeMobileMenu();
  }

  toggleCart() {
    console.log('Cart toggled');
  }

  toggleMobileMenu() {
    this.isMobileMenuOpen = !this.isMobileMenuOpen;
    this.isMobileSearchOpen = false;
  }

  toggleMobileSearch() {
    this.isMobileSearchOpen = !this.isMobileSearchOpen;
    this.isMobileMenuOpen = false;
  }

  closeMobileMenu() {
    this.isMobileMenuOpen = false;
    this.isMobileSearchOpen = false;
  }

  onMobileMenuClick(event: Event) {
    event.stopPropagation();
  }

  onSearchInput(event: any) {
    const query = event.target.value.trim();
    this.searchQuery = query;
    
    if (query.length < 2) {
      this.hideSearchDropdown();
      return;
    }
    
    this.searchSubject.next(query);
  }

  onSearchFocus() {
    if (this.searchQuery.length >= 2) {
      this.showSearchDropdown = true;
    }
  }

  onSearchBlur() {
    setTimeout(() => {
      this.hideSearchDropdown();
    }, 200);
  }

  performSearch() {
    if (this.searchQuery.trim()) {
      this.router.navigate(['/products'], {
        queryParams: { search: this.searchQuery.trim() }
      });
      this.hideSearchDropdown();
    }
  }

  selectProduct(product: Product) {
    this.router.navigate(['/products', product.id]);
    this.hideSearchDropdown();
    this.searchQuery = '';
  }

  private hideSearchDropdown() {
    this.showSearchDropdown = false;
    this.searchResults = [];
    this.showNoResults = false;
    this.isSearching = false;
  }

  onMobileSearchInput(event: any) {
    const query = event.target.value.trim();
    this.mobileSearchQuery = query;
    
    if (query.length < 2) {
      this.hideMobileSearchDropdown();
      return;
    }
    
    this.mobileSearchSubject.next(query);
  }

  performMobileSearch() {
    if (this.mobileSearchQuery.trim()) {
      this.router.navigate(['/products'], {
        queryParams: { search: this.mobileSearchQuery.trim() }
      });
      this.closeMobileMenu();
      this.mobileSearchQuery = '';
    }
  }

  selectMobileProduct(product: Product) {
    this.router.navigate(['/products', product.id]);
    this.closeMobileMenu();
    this.mobileSearchQuery = '';
  }

  private hideMobileSearchDropdown() {
    this.showMobileSearchDropdown = false;
    this.mobileSearchResults = [];
    this.showMobileNoResults = false;
    this.isMobileSearching = false;
  }
}
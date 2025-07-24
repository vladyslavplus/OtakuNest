import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { RouterModule } from '@angular/router';
import { ProductFilters } from '../../shared/components/product-filters/product-filters/product-filters';
import { ProductCard } from '../../shared/components/product-card/product-card';
import { Product } from '../../features/product/models/product.model';
import { PaginatedResult } from '../../core/pagination/paginated-result.model';
import { ProductService } from '../../features/product/services/product.service';
import { ProductQueryParams } from '../../features/product/models/query-params.model';

@Component({
  selector: 'app-products-page',
  imports: [CommonModule, RouterModule, ProductFilters, ProductCard],
  templateUrl: './products-page.html',
  styleUrl: './products-page.css'
})
export class ProductsPage implements OnInit {
  products: Product[] = [];
  pagination: PaginatedResult<Product[]>['pagination'] | null = null;
  isLoading: boolean = false;
  hasError: boolean = false;

  filters: ProductQueryParams = {
    pageNumber: 1,
    pageSize: 12
  };

  constructor(private productService: ProductService) { }

  ngOnInit(): void {
    this.onFiltersChanged({
      pageNumber: 1,
      pageSize: 12
    });
  }

  onFiltersChanged(filters: ProductQueryParams) {
    this.isLoading = true;
    this.hasError = false;
    this.filters = filters;

    this.productService.getProducts(filters).subscribe({
      next: (result) => {
        this.products = result.data;
        this.pagination = result.pagination;
        this.isLoading = false;
      },
      error: () => {
        this.products = [];
        this.pagination = null;
        this.isLoading = false;
        this.hasError = true;
      }
    });
  }

  changePage(page: number): void {
    if (this.pagination && (page < 1 || page > this.pagination.totalPages)) return;
    this.filters.pageNumber = page;
    this.onFiltersChanged(this.filters);
  }

  getVisiblePages(): (number | string)[] {
    if (!this.pagination) return [];

    const { currentPage, totalPages } = this.pagination;
    const pages: (number | string)[] = [];

    if (totalPages <= 7) {
      for (let i = 1; i <= totalPages; i++) {
        pages.push(i);
      }
      return pages;
    }

    pages.push(1);

    let startPage = Math.max(2, currentPage - 1);
    let endPage = Math.min(totalPages - 1, currentPage + 1);

    if (currentPage <= 3) {
      endPage = Math.min(5, totalPages - 1);
    }
    if (currentPage >= totalPages - 2) {
      startPage = Math.max(totalPages - 4, 2);
    }

    if (startPage > 2) {
      pages.push('...');
    }

    for (let i = startPage; i <= endPage; i++) {
      pages.push(i);
    }

    if (endPage < totalPages - 1) {
      pages.push('...');
    }

    if (totalPages > 1) {
      pages.push(totalPages);
    }

    return pages;
  }

  handlePageClick(page: number | string): void {
    if (typeof page === 'number') {
      this.changePage(page);
    }
  }
}

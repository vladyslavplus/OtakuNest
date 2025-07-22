import { Component, OnInit } from '@angular/core';
import { Product } from '../../../features/product/models/product.model';
import { ProductService } from '../../../features/product/services/product.service';
import { CommonModule } from '@angular/common';
import { HeroSection } from '../../../shared/components/hero-section/hero-section';
import { CategoryFilter } from '../../../shared/components/category-filter/category-filter';
import { ProductCard } from '../../../shared/components/product-card/product-card';
import { LoadingSpinner } from '../../../shared/components/loading-spinner/loading-spinner';
import { delay } from 'rxjs';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-home-page',
  standalone: true,
  imports: [CommonModule, RouterModule, HeroSection, CategoryFilter, ProductCard, LoadingSpinner],
  templateUrl: './home-page.html',
  styleUrl: './home-page.css'
})
export class HomePage implements OnInit{
  products: Product[] = [];
  loading = false;
  error = '';
  selectedCategory = 'Figures';

  constructor(private productService: ProductService) {}

  ngOnInit(): void {
    this.fetchProducts(this.selectedCategory);
  }

  onCategorySelected(category: string) {
    this.selectedCategory = category;
    this.fetchProducts(category);
  }

  private fetchProducts(category: string) {
    this.loading = true;
    this.error = '';
    this.productService.getProducts(category)
    .pipe(delay(500))
    .subscribe({
      next: (data) => {
        this.products = data;
        this.loading = false;
      },
      error: (err) => {
        this.error = 'Can\'t load the products.';
        this.loading = false;
      }
    });
  }
}

import { Component, EventEmitter, Output } from '@angular/core';
import { ProductQueryParams } from '../../../../features/product/models/query-params.model';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NgxSliderModule, Options } from '@angular-slider/ngx-slider';

@Component({
  selector: 'app-product-filters',
  standalone: true,
  imports: [CommonModule, FormsModule, NgxSliderModule],
  templateUrl: './product-filters.html',
  styleUrl: './product-filters.css'
})
export class ProductFilters {
  @Output() filtersChanged = new EventEmitter<ProductQueryParams>();

  maxPriceLimit = 1000;

  filters: ProductQueryParams = {
    category: '',
    minPrice: 0,
    maxPrice: this.maxPriceLimit,
    minRating: undefined,
    maxRating: undefined,
    minDiscount: undefined,
    maxDiscount: undefined,
    isAvailable: undefined
  };

  priceSliderOptions: Options = {
    floor: 0,
    ceil: this.maxPriceLimit,
    step: 1,
    hideLimitLabels: true,
    hidePointerLabels: true
  };

  setCategory(category: string): void {
    this.filters.category = category;
    this.emitFilters();
  }

  emitFilters() {
    if (
      this.filters.minPrice !== undefined &&
      this.filters.maxPrice !== undefined &&
      this.filters.minPrice > this.filters.maxPrice
    ) {
      [this.filters.minPrice, this.filters.maxPrice] = [this.filters.maxPrice, this.filters.minPrice];
    }

    this.filtersChanged.emit({ ...this.filters, pageNumber: 1, pageSize: 12 });
  }

  resetFilters() {
    this.filters = {
      category: '',
      minPrice: 0,
      maxPrice: this.maxPriceLimit,
      minRating: undefined,
      maxRating: undefined,
      minDiscount: undefined,
      maxDiscount: undefined,
      isAvailable: undefined
    };
    this.emitFilters();
  }

  onRatingInput(event: Event, field: 'minRating' | 'maxRating') {
    const target = event.target as HTMLInputElement;
    let value = parseFloat(target.value);

    if (isNaN(value)) {
      this.filters[field] = undefined;
      return;
    }

    value = Math.max(0, Math.min(5, value));
    this.filters[field] = value;
    target.value = value.toString();

    this.validateRating();
  }

  onDiscountInput(event: Event, field: 'minDiscount' | 'maxDiscount') {
    const target = event.target as HTMLInputElement;
    let value = parseFloat(target.value);

    if (isNaN(value)) {
      this.filters[field] = undefined;
      return;
    }

    value = Math.max(0, Math.min(100, Math.floor(value)));
    this.filters[field] = value;
    target.value = value.toString();

    this.validateDiscount();
  }

  validateRating() {
    if (this.filters.minRating != null) {
      this.filters.minRating = Math.max(0, Math.min(5, this.filters.minRating));
    }
    if (this.filters.maxRating != null) {
      this.filters.maxRating = Math.max(0, Math.min(5, this.filters.maxRating));
    }
    if (
      this.filters.minRating != null &&
      this.filters.maxRating != null &&
      this.filters.minRating > this.filters.maxRating
    ) {
      [this.filters.minRating, this.filters.maxRating] = [
        this.filters.maxRating,
        this.filters.minRating,
      ];
    }
    this.emitFilters();
  }

  validateDiscount() {
    if (this.filters.minDiscount != null) {
      this.filters.minDiscount = Math.max(0, Math.min(100, this.filters.minDiscount));
    }
    if (this.filters.maxDiscount != null) {
      this.filters.maxDiscount = Math.max(0, Math.min(100, this.filters.maxDiscount));
    }
    if (
      this.filters.minDiscount != null &&
      this.filters.maxDiscount != null &&
      this.filters.minDiscount > this.filters.maxDiscount
    ) {
      [this.filters.minDiscount, this.filters.maxDiscount] = [
        this.filters.maxDiscount,
        this.filters.minDiscount,
      ];
    }
    this.emitFilters();
  }
}

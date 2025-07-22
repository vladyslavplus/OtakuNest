import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output } from '@angular/core';

@Component({
  selector: 'app-category-filter',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './category-filter.html',
  styleUrl: './category-filter.css'
})
export class CategoryFilter {
  categories = ['Figures', 'Posters', 'Manga'];
  selectedCategory = 'Figures';

  @Output() categorySelected = new EventEmitter<string>();

  selectCategory(category: string, event?: Event) {
    if (event) {
      event.preventDefault();
      event.stopPropagation();
    }
    this.selectedCategory = category;
    this.categorySelected.emit(category);
  }
}

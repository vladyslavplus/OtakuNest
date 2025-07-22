import { ComponentFixture, TestBed } from '@angular/core/testing';

import { CategoryFilter } from './category-filter';

describe('CategoryFilter', () => {
  let component: CategoryFilter;
  let fixture: ComponentFixture<CategoryFilter>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CategoryFilter]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CategoryFilter);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

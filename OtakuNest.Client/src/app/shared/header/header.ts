import { Component, HostListener } from '@angular/core';
import { Router, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-header',
  imports: [RouterModule, CommonModule],
  templateUrl: './header.html',
  styleUrl: './header.css'
})
export class Header {
  isMobileMenuOpen = false;
  isMobileSearchOpen = false;
  isMobile = false;

  constructor(private router: Router) {
    this.checkScreenSize();
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: any) {
    this.checkScreenSize();
  }

  private checkScreenSize() {
    this.isMobile = window.innerWidth <= 640;
    if (!this.isMobile) {
      this.isMobileMenuOpen = false;
      this.isMobileSearchOpen = false;
    }
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
}
import { AfterViewInit, Component, ElementRef } from '@angular/core';

@Component({
  selector: 'app-hero-section',
  standalone: true,
  imports: [],
  templateUrl: './hero-section.html',
  styleUrl: './hero-section.css'
})
export class HeroSection implements AfterViewInit{
  
  constructor(private el: ElementRef) {}

  ngAfterViewInit(): void {
    let ticking = false;

    const updateScrollEffects = () => {
      const scrolled = window.scrollY;
      const heroSection = this.el.nativeElement.querySelector('.hero-section');
      const heroContent = this.el.nativeElement.querySelector('.hero-content');
      const heroSubtitle = this.el.nativeElement.querySelector('.hero-subtitle');
      const heroTitle = this.el.nativeElement.querySelector('.hero-title');

      if (heroSection) {
        const scrollProgress = Math.min(scrolled / 400, 1);
        const parallaxSpeed = scrolled * 0.3;

        heroSection.style.transform = `translateY(${parallaxSpeed}px)`;
        heroContent.style.transform = `translateY(${parallaxSpeed * 0.5}px)`;

        if (heroSubtitle) {
          heroSubtitle.style.opacity = Math.max(0, 1 - scrollProgress * 1.5).toString();
          heroSubtitle.style.transform = `translateY(${parallaxSpeed * 0.5}px) translateY(${scrollProgress * 15}px)`;
        }

        if (heroSection) {
          heroSection.style.filter = `blur(${scrollProgress * 6}px)`;
        }

        if (heroTitle) {
          heroTitle.style.opacity = Math.max(0.4, 1 - scrollProgress * 0.4).toString();
        }
      }

      ticking = false;
    };

    window.addEventListener('scroll', () => {
      if (!ticking) {
        requestAnimationFrame(updateScrollEffects);
        ticking = true;
      }
    });
  }
}

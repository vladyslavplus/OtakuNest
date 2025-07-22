import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './loading-spinner.html',
  styleUrl: './loading-spinner.css'
})
export class LoadingSpinner {
  @Input() message: string = '';
  @Input() size: 'small' | 'medium' | 'large' = 'medium';
  @Input() containerClass: string = '';
  @Input() spinnerType: 'classic' | 'gradient' | 'pulse' | 'dots' = 'classic';
}

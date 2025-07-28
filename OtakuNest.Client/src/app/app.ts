import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Header } from './shared/header/header';
import { Footer } from './shared/footer/footer';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [ RouterOutlet, Header, Footer],   
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class App {
  protected title = 'OtakuNest - Anime Merchandise Store';
}

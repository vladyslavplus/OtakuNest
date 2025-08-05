import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { CreateOrderDto } from '../models/create-order.dto';
import { Observable } from 'rxjs';
import { AuthService } from '../../user/services/auth.service';

@Injectable({
  providedIn: 'root'
})
export class OrderService {
  private readonly apiUrl = 'http://localhost:5000/api/Orders';

  constructor(
    private http: HttpClient,
    private authService: AuthService
  ) { }

  private getHeaders() {
    const token = this.authService.getAccessToken();
    return token
      ? { headers: { Authorization: `Bearer ${token}` } }
      : {};
  }

  createOrder(dto: CreateOrderDto): Observable<any> {
    return this.http.post(`${this.apiUrl}`, dto, this.getHeaders());
  }

  getOrderById(orderId: string): Observable<any> {
    return this.http.get(`${this.apiUrl}/${orderId}`, this.getHeaders());
  }

  getUserOrders(): Observable<any[]> {
    return this.http.get<any[]>(`${this.apiUrl}/my-orders`, this.getHeaders());
  }
}
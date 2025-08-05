import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { CreateOrderDto } from '../models/create-order.dto';
import { Observable } from 'rxjs';
import { AuthService } from '../../user/services/auth.service';
import { OrderDto } from '../models/order.dto';
import { OrderParameters } from '../../../core/params/order-parameters';
import { PaginatedResult } from '../../../core/pagination/paginated-result.model';
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

  getOrderById(orderId: string): Observable<OrderDto> {
    return this.http.get<OrderDto>(`${this.apiUrl}/${orderId}`, this.getHeaders());
  }

  getUserOrders(params?: OrderParameters): Observable<PaginatedResult<OrderDto[]>> {
    let httpParams = new HttpParams();
  
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        if (
          value !== undefined &&
          value !== null &&
          value !== '' &&
          key.toLowerCase() !== 'userid' 
        ) {
          httpParams = httpParams.set(key, value.toString());
        }
      });
    }
  
    return new Observable(observer => {
      this.http.get<OrderDto[]>(`${this.apiUrl}/me`, {
        ...this.getHeaders(),
        params: httpParams,
        observe: 'response'
      }).subscribe({
        next: response => {
          const paginationHeader = response.headers.get('Pagination');
          const pagination = paginationHeader ? JSON.parse(paginationHeader) : null;
  
          const result: PaginatedResult<OrderDto[]> = {
            data: response.body || [],
            pagination: pagination
          };
  
          observer.next(result);
          observer.complete();
        },
        error: error => observer.error(error)
      });
    });
  }  
}

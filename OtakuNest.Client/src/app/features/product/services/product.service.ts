import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Product } from '../models/product.model';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private apiUrl = 'http://localhost:5000/api/products'; 

  constructor(private http: HttpClient) {}

  getProducts(category: string, pageSize: number = 6): Observable<Product[]> {
    const url = `${this.apiUrl}?Category=${category}&PageSize=${pageSize}`;
    return this.http.get<Product[]>(url);
  }  

  getProductById(id: string): Observable<Product> {
    return this.http.get<Product>(`${this.apiUrl}/${id}`);
  }
}

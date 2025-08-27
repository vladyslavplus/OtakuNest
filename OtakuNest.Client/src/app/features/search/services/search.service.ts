import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ProductElasticDto } from '../models/product-elastic.dto';

@Injectable({
  providedIn: 'root'
})
export class SearchService {
  private apiUrl = 'http://localhost:5000/api/search';
  
  constructor(private http: HttpClient) {}

  searchProducts(query: string, page: number = 1, size: number = 20): Observable<ProductElasticDto[]> {
    return this.http.get<ProductElasticDto[]>(`${this.apiUrl}/products`, {
      params: {
        query: query,
        page: page.toString(),
        size: size.toString()
      }
    });
  }
}

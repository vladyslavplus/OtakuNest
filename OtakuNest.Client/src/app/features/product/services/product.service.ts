import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { Product } from '../models/product.model';
import { ProductParameters } from '../../../core/params/product-parameters';
import { PaginatedResult } from '../../../core/pagination/paginated-result.model';

@Injectable({
  providedIn: 'root'
})
export class ProductService {
  private apiUrl = 'http://localhost:5000/api/products'; 

  constructor(private http: HttpClient) {}

  getProducts(params : ProductParameters): Observable<PaginatedResult<Product[]>> {
    let httpParams = new HttpParams();

    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        httpParams = httpParams.set(key, value.toString());
      }
    });

    return this.http.get<Product[]>(this.apiUrl, {
      params: httpParams,
      observe: 'response'
    }).pipe(
      map((response: HttpResponse<Product[]>) => {
        const paginatedResult: PaginatedResult<Product[]> = {
          data: response.body || [],
          pagination: JSON.parse(response.headers.get('Pagination') || '{}')
        };
        return paginatedResult;
      })
    );
  }  

  getProductById(id: string): Observable<Product> {
    return this.http.get<Product>(`${this.apiUrl}/${id}`);
  }
}

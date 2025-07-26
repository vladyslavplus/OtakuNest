import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { ApplicationUser } from '../models/application-user.model';
import { Observable } from 'rxjs';
import { UpdateUserDto } from '../models/update-user.dto';

@Injectable({
  providedIn: 'root'
})
export class UserService {
  private readonly baseUrl = 'http://localhost:5000/api/Users'; 

  constructor(private http: HttpClient) {}

  getUserById(id: string): Observable<ApplicationUser> {
    return this.http.get<ApplicationUser>(`${this.baseUrl}/${id}`);
  }

  updateUser(id: string, dto: UpdateUserDto): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, dto);
  }
}

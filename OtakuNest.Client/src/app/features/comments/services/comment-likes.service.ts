import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class CommentLikesService {
  private readonly baseUrl = 'http://localhost:5000/api/Comments';

  constructor(private http: HttpClient) {}

  getLikesCount(commentId: string): Observable<number> {
    return this.http.get<number>(`${this.baseUrl}/${commentId}/likes/count`);
  }

  hasUserLiked(commentId: string): Observable<boolean> {
    return this.http.get<boolean>(`${this.baseUrl}/${commentId}/likes/user`);
  }

  addLike(commentId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${commentId}/likes`, {});
  }

  removeLike(commentId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${commentId}/likes`);
  }
}

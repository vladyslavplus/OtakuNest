import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { CommentParameters } from '../../../core/params/comment-parameters';
import { map, Observable } from 'rxjs';
import { PaginatedResult } from '../../../core/pagination/paginated-result.model';
import { CommentDto } from '../models/comment.dto';
import { CreateCommentDto } from '../models/create-comment.dto';
import { ReplyToCommentDto } from '../models/reply-to-comment.dto';
import { UpdateCommentDto } from '../models/update-comment.dto';

@Injectable({
  providedIn: 'root'
})
export class CommentService {
  private apiUrl = 'http://localhost:5000/api/Comments';

  constructor(private http: HttpClient) {}

  getComments(params: CommentParameters): Observable<PaginatedResult<CommentDto[]>> {
    let httpParams = new HttpParams();

    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        httpParams = httpParams.set(key, value.toString());
      }
    });

    return this.http.get<CommentDto[]>(this.apiUrl, {
      params: httpParams,
      observe: 'response'
    }).pipe(
      map((response: HttpResponse<CommentDto[]>) => {
        const paginatedResult: PaginatedResult<CommentDto[]> = {
          data: response.body || [],
          pagination: JSON.parse(response.headers.get('Pagination') || '{}')
        };
        return paginatedResult;
      })
    );
  }

  getCommentById(id: string): Observable<CommentDto> {
    return this.http.get<CommentDto>(`${this.apiUrl}/${id}`);
  }

  createComment(dto: CreateCommentDto): Observable<CommentDto> {
    return this.http.post<CommentDto>(this.apiUrl, dto);
  }

  replyToComment(dto: ReplyToCommentDto): Observable<CommentDto> {
    return this.http.post<CommentDto>(`${this.apiUrl}/reply`, dto);
  }

  updateComment(id: string, dto: UpdateCommentDto): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/${id}`, dto);
  }

  deleteComment(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}

import { Component, Input, OnInit, OnDestroy } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, takeUntil, finalize, forkJoin, of } from 'rxjs';
import { switchMap, catchError } from 'rxjs/operators';
import { CommentParameters } from '../../../core/params/comment-parameters';
import { PaginatedResult } from '../../../core/pagination/paginated-result.model';
import { CommentDto } from '../../../features/comments/models/comment.dto';
import { ReplyDto } from '../../../features/comments/models/reply.dto';
import { CommentService } from '../../../features/comments/services/comment.service';
import { CommentLikesService } from '../../../features/comments/services/comment-likes.service';
import { CreateCommentDto } from '../../../features/comments/models/create-comment.dto';
import { ReplyToCommentDto } from '../../../features/comments/models/reply-to-comment.dto';
import { UpdateCommentDto } from '../../../features/comments/models/update-comment.dto';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../features/user/services/auth.service';

interface CommentFormData {
  content: string;
}

interface ReplyFormState {
  [commentId: string]: FormGroup;
}

interface EditFormState {
  [commentId: string]: {
    form: FormGroup;
    originalContent: string;
  };
}

interface LikeState {
  [commentId: string]: {
    isLiked: boolean;
    likesCount: number;
    isLoading: boolean;
  };
}

@Component({
  imports: [CommonModule, ReactiveFormsModule],
  selector: 'app-comment-section',
  templateUrl: './comment-section.html',
  styleUrls: ['./comment-section.css']
})
export class CommentSection implements OnInit, OnDestroy {
  @Input() productId!: string;
  @Input() currentUserId?: string;
  @Input() isAuthenticated: boolean = false;

  currentUserName: string | null = null;
  comments: CommentDto[] = [];
  pagination: PaginatedResult<CommentDto[]>['pagination'] | null = null;

  mainCommentForm: FormGroup;
  replyForms: ReplyFormState = {};
  editForms: EditFormState = {};
  likeStates: LikeState = {};

  isLoading = false;
  isSubmitting = false;
  activeReplyId: string | null = null;
  activeEditId: string | null = null;
  expandedReplies: Set<string> = new Set();

  commentParameters: CommentParameters;

  private destroy$ = new Subject<void>();

  constructor(
    private commentService: CommentService,
    private commentLikesService: CommentLikesService,
    private authService: AuthService,
    private fb: FormBuilder
  ) {
    this.mainCommentForm = this.createCommentForm();
    this.commentParameters = new CommentParameters();
  }

  ngOnInit(): void {
    if (!this.productId) {
      console.error('ProductId is required for comments section');
      return;
    }

    this.currentUserName = this.authService.getCurrentUserName();
    console.log(this.currentUserName);

    this.commentParameters.productId = this.productId;
    this.loadComments();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private createCommentForm(): FormGroup {
    return this.fb.group({
      content: ['', [Validators.required, Validators.minLength(1), Validators.maxLength(1000)]]
    });
  }

  loadComments(resetPage: boolean = false): void {
    if (resetPage) {
      this.commentParameters.pageNumber = 1;
    }

    this.isLoading = true;
    this.commentService.getComments(this.commentParameters)
      .pipe(
        takeUntil(this.destroy$),
        switchMap((result: PaginatedResult<CommentDto[]>) => {
          if (resetPage) {
            this.comments = result.data;
          } else {
            this.comments = [...this.comments, ...result.data];
          }
          this.pagination = result.pagination;

          return this.loadLikesForComments(result.data);
        }),
        finalize(() => this.isLoading = false)
      )
      .subscribe({
        next: () => {
          console.log('Comments and likes loaded successfully');
        },
        error: (error) => {
          console.error('Error loading comments:', error);
        }
      });
  }

  private loadLikesForComments(comments: CommentDto[]) {
    const likesRequests: any[] = [];

    const allCommentIds = this.getAllCommentIds(comments);

    if (!this.isAuthenticated || allCommentIds.length === 0) {
      return of(null);
    }

    allCommentIds.forEach(commentId => {
      this.likeStates[commentId] = {
        isLiked: false,
        likesCount: 0,
        isLoading: false
      };

      const likesCount$ = this.commentLikesService.getLikesCount(commentId).pipe(
        catchError(() => of(0))
      );

      const userLiked$ = this.commentLikesService.hasUserLiked(commentId).pipe(
        catchError(() => of(false))
      );

      likesRequests.push(
        forkJoin([likesCount$, userLiked$]).pipe(
          takeUntil(this.destroy$)
        ).subscribe({
          next: ([likesCount, isLiked]) => {
            this.likeStates[commentId] = {
              isLiked,
              likesCount,
              isLoading: false
            };
          },
          error: (error) => {
            console.error(`Error loading likes for comment ${commentId}:`, error);
          }
        })
      );
    });

    return of(null);
  }

  private getAllCommentIds(comments: CommentDto[]): string[] {
    const ids: string[] = [];

    comments.forEach(comment => {
      ids.push(comment.id);
      if (comment.replies) {
        ids.push(...this.getReplyIds(comment.replies));
      }
    });

    return ids;
  }

  private getReplyIds(replies: ReplyDto[]): string[] {
    const ids: string[] = [];

    replies.forEach(reply => {
      ids.push(reply.id);
      if (reply.replies) {
        ids.push(...this.getReplyIds(reply.replies));
      }
    });

    return ids;
  }

  loadMoreComments(): void {
    if (this.pagination && this.pagination.currentPage < this.pagination.totalPages) {
      this.commentParameters.pageNumber++;
      this.loadComments();
    }
  }

  submitMainComment(): void {
    if (!this.mainCommentForm.valid || !this.isAuthenticated) {
      return;
    }

    const formData = this.mainCommentForm.value as CommentFormData;
    const createDto: CreateCommentDto = {
      productId: this.productId,
      content: formData.content.trim()
    };

    this.isSubmitting = true;
    this.commentService.createComment(createDto)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.isSubmitting = false)
      )
      .subscribe({
        next: (newComment: CommentDto) => {
          newComment.userName = this.currentUserName || 'Unknown';

          this.comments.unshift(newComment);
          this.mainCommentForm.reset();

          this.likeStates[newComment.id] = {
            isLiked: false,
            likesCount: 0,
            isLoading: false
          };

          if (this.pagination) {
            this.pagination.totalCount++;
          }
        },
        error: (error) => {
          console.error('Error creating comment:', error);
        }
      });
  }

  toggleLike(commentId: string): void {
    if (!this.isAuthenticated || this.likeStates[commentId]?.isLoading) {
      return;
    }

    const currentState = this.likeStates[commentId];
    if (!currentState) {
      return;
    }

    this.likeStates[commentId] = {
      ...currentState,
      isLoading: true
    };

    const likeAction = currentState.isLiked
      ? this.commentLikesService.removeLike(commentId)
      : this.commentLikesService.addLike(commentId);

    likeAction.pipe(
      takeUntil(this.destroy$),
      finalize(() => {
        this.likeStates[commentId].isLoading = false;
      })
    ).subscribe({
      next: () => {
        this.likeStates[commentId] = {
          ...this.likeStates[commentId],
          isLiked: !currentState.isLiked,
          likesCount: currentState.isLiked
            ? currentState.likesCount - 1
            : currentState.likesCount + 1
        };
      },
      error: (error) => {
        console.error('Error toggling like:', error);
        this.likeStates[commentId] = {
          ...currentState,
          isLoading: false
        };
      }
    });
  }

  getLikeState(commentId: string) {
    return this.likeStates[commentId] || {
      isLiked: false,
      likesCount: 0,
      isLoading: false
    };
  }

  showReplyForm(commentId: string): void {
    this.activeReplyId = commentId;
    this.activeEditId = null;

    if (!this.replyForms[commentId]) {
      this.replyForms[commentId] = this.createCommentForm();
    }
  }

  hideReplyForm(): void {
    this.activeReplyId = null;
  }

  submitReply(parentId: string): void {
    const replyForm = this.replyForms[parentId];
    if (!replyForm?.valid || !this.isAuthenticated) {
      return;
    }

    const formData = replyForm.value as CommentFormData;
    const rootCommentId = this.findRootCommentId(parentId);

    const replyDto: ReplyToCommentDto = {
      parentCommentId: parentId,
      productId: this.productId,
      content: formData.content.trim()
    };

    this.isSubmitting = true;
    this.commentService.replyToComment(replyDto)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.isSubmitting = false)
      )
      .subscribe({
        next: (newReply: CommentDto) => {
          console.log('New reply from server:', newReply);
          this.addReplyToTree(newReply, parentId);

          this.likeStates[newReply.id] = {
            isLiked: false,
            likesCount: 0,
            isLoading: false
          };

          replyForm.reset();
          this.hideReplyForm();

          if (rootCommentId) {
            this.expandedReplies.add(rootCommentId);
          }
        },
        error: (error) => {
          console.error('Error creating reply:', error);
          alert(error.error?.message || 'Failed to create reply');
        }
      });
  }

  private findRootCommentId(commentId: string): string | null {
    for (const comment of this.comments) {
      if (comment.id === commentId) {
        return comment.id;
      }

      if (this.isReplyInComment(comment.replies || [], commentId)) {
        return comment.id;
      }
    }
    return null;
  }

  private isReplyInComment(replies: ReplyDto[], targetId: string): boolean {
    for (const reply of replies) {
      if (reply.id === targetId) {
        return true;
      }
      if (reply.replies && reply.replies.length > 0) {
        if (this.isReplyInComment(reply.replies, targetId)) {
          return true;
        }
      }
    }
    return false;
  }

  private addReplyToTree(newReply: CommentDto, parentId: string): void {
    const newReplyDto: ReplyDto = {
      id: newReply.id,
      userId: newReply.userId,
      userName: newReply.userName,
      content: newReply.content,
      createdAt: newReply.createdAt,
      updatedAt: newReply.updatedAt,
      likesCount: newReply.likesCount,
      replies: newReply.replies || []
    };

    for (const comment of this.comments) {
      if (comment.id === parentId) {
        comment.replies = comment.replies || [];
        comment.replies.push(newReplyDto);
        return;
      }

      if (this.addReplyToReplies(comment.replies || [], parentId, newReplyDto)) {
        return;
      }
    }
  }

  private addReplyToReplies(replies: ReplyDto[], parentId: string, newReply: ReplyDto): boolean {
    for (const reply of replies) {
      if (reply.id === parentId) {
        reply.replies = reply.replies || [];
        reply.replies.push(newReply);
        return true;
      }

      if (reply.replies && reply.replies.length > 0) {
        if (this.addReplyToReplies(reply.replies, parentId, newReply)) {
          return true;
        }
      }
    }
    return false;
  }

  startEdit(comment: CommentDto | ReplyDto): void {
    this.activeEditId = comment.id;
    this.activeReplyId = null;

    const editForm = this.createCommentForm();
    editForm.patchValue({ content: comment.content });

    this.editForms[comment.id] = {
      form: editForm,
      originalContent: comment.content
    };
  }

  cancelEdit(commentId: string): void {
    delete this.editForms[commentId];
    this.activeEditId = null;
  }

  submitEdit(commentId: string): void {
    const editState = this.editForms[commentId];
    if (!editState?.form.valid) {
      return;
    }

    const formData = editState.form.value as CommentFormData;
    const updateDto: UpdateCommentDto = {
      content: formData.content.trim()
    };

    this.isSubmitting = true;
    this.commentService.updateComment(commentId, updateDto)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.isSubmitting = false)
      )
      .subscribe({
        next: () => {
          const item = this.findCommentOrReplyById(commentId);
          if (item) {
            item.content = updateDto.content;
            item.updatedAt = new Date().toISOString();
          }

          this.cancelEdit(commentId);
        },
        error: (error) => {
          console.error('Error updating comment:', error);
        }
      });
  }

  deleteComment(commentId: string): void {
    if (!confirm('Are you sure you want to delete this comment?')) {
      return;
    }

    this.commentService.deleteComment(commentId)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.removeCommentFromArray(commentId);
          delete this.likeStates[commentId];

          if (this.pagination) {
            this.pagination.totalCount--;
          }
        },
        error: (error) => {
          console.error('Error deleting comment:', error);
        }
      });
  }

  toggleReplies(commentId: string): void {
    if (this.expandedReplies.has(commentId)) {
      this.expandedReplies.delete(commentId);
    } else {
      this.expandedReplies.add(commentId);
    }
  }

  isRepliesExpanded(commentId: string): boolean {
    return this.expandedReplies.has(commentId);
  }

  hasReplies(reply: ReplyDto): boolean {
    return reply.replies && reply.replies.length > 0;
  }

  getRepliesCount(reply: ReplyDto): number {
    return reply.replies ? reply.replies.length : 0;
  }

  private findCommentOrReplyById(commentId: string): CommentDto | ReplyDto | undefined {
    for (const comment of this.comments) {
      if (comment.id === commentId) {
        return comment;
      }

      const found = this.findInReplies(comment.replies || [], commentId);
      if (found) {
        return found;
      }
    }
    return undefined;
  }

  private findInReplies(replies: ReplyDto[], commentId: string): ReplyDto | undefined {
    for (const reply of replies) {
      if (reply.id === commentId) {
        return reply;
      }

      if (reply.replies && reply.replies.length > 0) {
        const found = this.findInReplies(reply.replies, commentId);
        if (found) {
          return found;
        }
      }
    }
    return undefined;
  }

  private removeCommentFromArray(commentId: string): void {
    this.comments = this.comments.filter(c => c.id !== commentId);

    this.comments.forEach(comment => {
      if (comment.replies) {
        comment.replies = this.removeFromReplies(comment.replies, commentId);
      }
    });
  }

  private removeFromReplies(replies: ReplyDto[], commentId: string): ReplyDto[] {
    return replies
      .filter(r => r.id !== commentId)
      .map(r => ({
        ...r,
        replies: r.replies ? this.removeFromReplies(r.replies, commentId) : []
      }));
  }

  canEditComment(comment: CommentDto | ReplyDto): boolean {
    return this.isAuthenticated && comment.userId === this.currentUserId;
  }

  canDeleteComment(comment: CommentDto | ReplyDto): boolean {
    return this.isAuthenticated && comment.userId === this.currentUserId;
  }

  get hasMoreComments(): boolean {
    return this.pagination ? this.pagination.currentPage < this.pagination.totalPages : false;
  }

  get totalComments(): number {
    return this.comments.length;
  }

  getReplyForm(commentId: string): FormGroup | undefined {
    return this.replyForms[commentId];
  }

  getEditForm(commentId: string): FormGroup | undefined {
    return this.editForms[commentId]?.form;
  }

  isActiveReply(commentId: string): boolean {
    return this.activeReplyId === commentId;
  }

  isActiveEdit(commentId: string): boolean {
    return this.activeEditId === commentId;
  }
}
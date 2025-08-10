import { ReplyDto } from "./reply.dto";

export interface CommentDto {
    id: string;
    productId: string;
    userId: string;
    userName?: string;
    content: string;
    createdAt: string;
    updatedAt?: string;
    parentCommentId?: string;
    likesCount: number;
    replies: ReplyDto[];
}
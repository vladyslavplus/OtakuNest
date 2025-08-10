export interface ReplyDto {
    id: string;
    userId: string;
    userName?: string;
    content: string;
    createdAt: string;
    updatedAt?: string;
    likesCount: number;
    replies: ReplyDto[];
}

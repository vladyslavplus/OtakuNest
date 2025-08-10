import { QueryStringParameters } from "./query-string-parameters";

export class CommentParameters extends QueryStringParameters {
    productId!: string;
    parentCommentId?: string;
    content?: string;

    constructor() {
        super();
        this.orderBy = 'CreatedAt desc';
    }
}
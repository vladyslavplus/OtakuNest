import { QueryStringParameters } from './query-string-parameters';

export class OrderParameters extends QueryStringParameters {
    status?: string;
    userId?: string;
    minPrice?: number;
    maxPrice?: number;
    fromDate?: string;
    toDate?: string;
    productId?: string;

    constructor() {
        super();
        this.orderBy = 'CreatedAt desc'; 
    }
}

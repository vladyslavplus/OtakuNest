export interface ProductQueryParams {
    name?: string;
    category?: string;
    minPrice?: number;
    maxPrice?: number;
    isAvailable?: boolean;
    sku?: string;
    minRating?: number;
    maxRating?: number;
    minDiscount?: number;
    maxDiscount?: number;
    pageNumber?: number;
    pageSize?: number;
    orderBy?: string;
}

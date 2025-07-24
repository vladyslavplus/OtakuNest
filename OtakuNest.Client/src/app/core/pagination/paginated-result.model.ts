export interface PaginatedResult<T> {
    data: T;
    pagination: {
        totalCount: number;
        pageSize: number;
        currentPage: number;
        totalPages: number;
        hasNext: boolean;
        hasPrevious: boolean;
    };
}

export interface RateLimitInfo {
    requestCount: number;
    windowStart: number;
    isBlocked: boolean;
    blockEndTime: number;
}
export interface DecodedToken {
    sub: string;
    email: string;
    exp: number;
    userId: string;
    [key: string]: any;
}

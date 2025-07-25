export interface DecodedToken {
    sub: string;
    email: string;
    exp: number;
    [key: string]: any;
}
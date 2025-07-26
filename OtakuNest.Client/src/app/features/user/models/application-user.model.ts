export interface ApplicationUser {
    id: string;
    userName: string;
    email: string;
    phoneNumber?: string;
    emailConfirmed: boolean;
    phoneNumberConfirmed: boolean;
    createdAt: string;
}
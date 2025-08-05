export interface OrderItemDto {
    productId: string;
    productName?: string,
    quantity: number;
    unitPrice: number;
    totalPrice: number;
}
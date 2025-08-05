export interface CreateOrderDto {
    shippingAddress: string;
    items: {
        productId: string;
        quantity: number;
    }[];
}
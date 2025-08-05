import { OrderItemDto } from "./order-item.dto";

export interface OrderDto {
    id: string;               
    userId: string;           
    createdAt: string;       
    status: string;
    items: OrderItemDto[];
    totalPrice: number;
    shippingAddress: string;
}
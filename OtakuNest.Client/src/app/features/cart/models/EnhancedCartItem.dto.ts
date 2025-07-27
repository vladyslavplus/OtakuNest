import { Product } from "../../product/models/product.model";
import { CartItemDto } from "./CartItemDto.model";

export interface EnhancedCartItem extends CartItemDto {
    product?: Product;
    totalPrice: number;
    loading?: boolean;
}
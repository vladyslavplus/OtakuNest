import { Routes } from '@angular/router';
import { HomePage } from './pages/home/home-page/home-page';
import { ProductsPage } from './pages/products-page/products-page';
import { AuthPage } from './pages/auth-page/auth-page';
import { AccountPage } from './pages/account-page/account-page';
import { CartPage } from './pages/cart-page/cart-page';
import { ProductDetailPage } from './pages/product-detail-page/product-detail-page';

export const routes: Routes = [
    {
        path: '',
        component: HomePage
    }, 
    {
        path: 'products',
        component: ProductsPage 
    }, 
    {
        path: 'products/:id',
        component: ProductDetailPage
    },
    {
        path: 'cart',
        component: CartPage
    }, 
    {
        path: 'auth',
        component: AuthPage
    },
    {
        path: 'account',
        component: AccountPage
    },
    {
        path: '**',
        redirectTo: ''
    }
];

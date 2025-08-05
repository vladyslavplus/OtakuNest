export interface OrderStage {
    id: string;
    label: string;
    status: 'pending' | 'active' | 'completed';
    icon: string;
}
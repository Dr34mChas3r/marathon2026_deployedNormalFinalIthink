import { OrderItem } from '../dishes/order-item.model';

export interface Order {
  orderId: string;
  reservationAddress: string;
  reservationTime: string;
  reservationId: string;
  orderItems: OrderItem[];
  status: number;
}

export enum OrderStatus {
  Created = 0,
  Submitted = 1,
  Cancelled = 2,
  SubmittedByCustomer = 3,
  Preparing = 4,
  Ready = 5,
  Served = 6,
}

export interface WaiterPreOrderState {
  reservationId: string;
  createdByName: string;
  createdByRole: string;
  tableName: string;
}

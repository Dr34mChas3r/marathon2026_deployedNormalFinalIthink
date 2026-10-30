import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { OrderStatus } from '@app/models/orders/order.model';
import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { TableService } from '@app/services/table/table.service';
import { ToastService } from '@app/services/toast/toast.service';
import { WaitersService } from '@app/services/waiters/waiters.service';
import { KitchenDisplayComponent } from './kitchen-display.component';

describe('KitchenDisplayComponent', () => {
  let fixture: ComponentFixture<KitchenDisplayComponent>;
  let reservationService: jasmine.SpyObj<ReservationService>;
  let ordersService: jasmine.SpyObj<PreOrdersService>;

  const reservation: Reservation = {
    id: 'reservation-1',
    tableId: 'table-1',
    customerId: 'customer-1',
    startTime: new Date('2026-10-30T12:00:00Z'),
    endTime: new Date('2026-10-30T13:00:00Z'),
    guestCount: 2,
    status: ReservationStatus.InProggress,
    locationAddress: 'Restaurant',
    tableCapacity: 2,
    waiterId: 'waiter-1',
    preOrder: {
      orderId: 'order-1',
      reservationId: 'reservation-1',
      reservationAddress: 'Restaurant, Table 1',
      reservationTime: '12:00',
      status: OrderStatus.SubmittedByCustomer,
      orderItems: [{
        dish: {
          id: 'dish-1',
          name: 'Soup',
          imageUrl: '',
          price: 10,
          state: 'Available',
          weight: '200g',
          dishType: 'MainCourse',
          description: '',
          calories: '',
          proteins: '',
          fats: '',
          carbohydrates: '',
          vitamins: '',
        },
        quantity: 2,
        state: 0,
      }],
    },
  };

  beforeEach(() => {
    reservationService = jasmine.createSpyObj<ReservationService>('ReservationService', ['getReservationsForUser$']);
    ordersService = jasmine.createSpyObj<PreOrdersService>('PreOrdersService', ['getByReservationId', 'updateKitchenStatus']);
    reservationService.getReservationsForUser$.and.returnValue(of([{ ...reservation, preOrder: undefined }]));
    ordersService.getByReservationId.and.returnValue(of(reservation.preOrder!));
    ordersService.updateKitchenStatus.and.returnValue(of({ status: OrderStatus.Preparing }));

    TestBed.configureTestingModule({
      imports: [KitchenDisplayComponent],
      providers: [
        { provide: AuthenticationService, useValue: { getUser: () => ({ id: 'waiter-1', role: 'Waiter' }) } },
        { provide: ReservationService, useValue: reservationService },
        { provide: PreOrdersService, useValue: ordersService },
        { provide: TableService, useValue: { getLocationTables$: () => of([{ id: 'table-1', tableNumber: 1 }]) } },
        { provide: WaitersService, useValue: { getWaiterLocationId: () => of('location-1') } },
        { provide: ToastService, useValue: { success: jasmine.createSpy(), error: jasmine.createSpy() } },
      ],
    });
    fixture = TestBed.createComponent(KitchenDisplayComponent);
    fixture.detectChanges();
  });

  it('shows an active submitted reservation and advances it to preparing', () => {
    expect(fixture.nativeElement.textContent).toContain('Table 1');
    expect(fixture.nativeElement.textContent).toContain('Soup');
    expect(fixture.nativeElement.textContent).toContain('Start preparing');

    fixture.nativeElement.querySelector('.action-button').click();
    fixture.detectChanges();

    expect(ordersService.updateKitchenStatus).toHaveBeenCalledWith(
      'reservation-1',
      OrderStatus.Preparing
    );
    expect(fixture.nativeElement.textContent).toContain('Preparing');
  });

  it('does not queue cancelled reservations', () => {
    fixture.componentInstance.reservations.set([{
      ...reservation,
      status: ReservationStatus.Cancelled,
    }]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No orders to prepare');
  });

  it('loads each reservation order before building the kitchen queue', () => {
    expect(ordersService.getByReservationId).toHaveBeenCalledWith('reservation-1');
    expect(fixture.nativeElement.textContent).toContain('Soup');
  });

  it('treats a missing reservation order as an empty queue entry', () => {
    ordersService.getByReservationId.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 404 }))
    );

    fixture.componentInstance.refresh();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No orders to prepare');
    expect(fixture.componentInstance.error()).toBeNull();
  });

  it('keeps the previous tickets visible when a refresh fails', () => {
    reservationService.getReservationsForUser$.and.returnValue(
      throwError(() => new Error('network error'))
    );

    fixture.componentInstance.refresh();
    fixture.detectChanges();

    expect(fixture.componentInstance.refreshError()).toBeTrue();
    expect(fixture.nativeElement.textContent).toContain('Couldn’t refresh');
    expect(fixture.nativeElement.textContent).toContain('Soup');
  });
});

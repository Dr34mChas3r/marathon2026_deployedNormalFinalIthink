import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of } from 'rxjs';

import { Order } from '@app/models/orders/order.model';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ReservationService } from '@app/services/reservation/reservation.service';

import { CartComponent } from './cart.component';

describe('CartComponent', () => {
  let component: CartComponent;
  let fixture: ComponentFixture<CartComponent>;

  let preOrdersServiceMock: jasmine.SpyObj<PreOrdersService>;
  let reservationServiceMock: jasmine.SpyObj<ReservationService>;
  let routerMock: jasmine.SpyObj<Router>;

  beforeEach(async () => {
    preOrdersServiceMock =
      jasmine.createSpyObj<PreOrdersService>(
        'PreOrdersService',
        [
          'getPreOrders',
          'cancelPreOrderByReservationId',
          'unsubmitPreOrderByReservationId',
          'submitPreOrderByReservationId',
          'editPreOrderByReservationId',
        ],
      );

    routerMock = jasmine.createSpyObj<Router>(
      'Router',
      ['navigate'],
    );

    preOrdersServiceMock.getPreOrders.and.returnValue(of([]));
    reservationServiceMock = jasmine.createSpyObj<ReservationService>(
      'ReservationService',
      ['getReservationsForUser$']
    );
    reservationServiceMock.getReservationsForUser$.and.returnValue(of([]));

    await TestBed.configureTestingModule({
      imports: [CartComponent],
      providers: [
        {
          provide: PreOrdersService,
          useValue: preOrdersServiceMock,
        },
        {
          provide: Router,
          useValue: routerMock,
        },
        {
          provide: ReservationService,
          useValue: reservationServiceMock,
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CartComponent);
    component = fixture.componentInstance;

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit closeCartEvent when closeCart is called', () => {
    spyOn(component.closeCartEvent, 'emit');

    component.closeCart();

    expect(
      component.closeCartEvent.emit,
    ).toHaveBeenCalled();
  });

  it('should return submitted orders with statuses 1 and 3', () => {
    component.orders.set([
      {
        orderId: 'order-1',
        status: 1,
      },
      {
        orderId: 'order-2',
        status: 2,
      },
      {
        orderId: 'order-3',
        status: 3,
      },
      {
        orderId: 'order-4',
        status: 0,
      },
    ] as Order[]);

    expect(
      component
        .submittedOrders()
        .map((order) => order.status),
    ).toEqual([1, 3]);
  });

  it('should return unsubmitted orders with status 0', () => {
    component.orders.set([
      {
        orderId: 'order-1',
        status: 0,
      },
      {
        orderId: 'order-2',
        status: 1,
      },
      {
        orderId: 'order-3',
        status: 3,
      },
    ] as Order[]);

    expect(
      component
        .unsubmittedOrders()
        .map((order) => order.status),
    ).toEqual([0]);
  });

  it('uses the current reservation time instead of the stale order snapshot', () => {
    const order = {
      orderId: 'order-1',
      reservationId: 'reservation-1',
      reservationAddress: 'Restaurant',
      reservationTime: 'Oct 30, 2026, 4:30 PM - 6:00 PM',
      status: 3,
      orderItems: [],
    } as Order;
    preOrdersServiceMock.getPreOrders.and.returnValue(of([order]));
    reservationServiceMock.getReservationsForUser$.and.returnValue(of([{
      id: 'reservation-1',
      tableId: 'table-5',
      customerId: 'customer-1',
      startTime: new Date('2026-10-30T16:30:00Z'),
      endTime: new Date('2026-10-30T18:00:00Z'),
      guestCount: 1,
      status: 0,
      locationAddress: 'Restaurant',
      tableCapacity: 6,
      waiterId: 'waiter-1',
    }]));

    component.ngOnInit();

    expect(component.orders()[0].reservationStartTime).toEqual(new Date('2026-10-30T16:30:00Z'));
    expect(component.orders()[0].reservationEndTime).toEqual(new Date('2026-10-30T18:00:00Z'));
  });

  it('should navigate to reservations when goToReservations is called', () => {
    component.goToReservations();

    expect(routerMock.navigate).toHaveBeenCalledWith([
      '/reservations',
    ]);
  });
});

import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { catchError, forkJoin, interval, map, of, switchMap, throwError } from 'rxjs';
import { AuthenticationService } from '@app/auth/services/authentication.service';
import { Reservation, ReservationStatus } from '@app/models/reservations/reservation.model';
import { OrderStatus } from '@app/models/orders/order.model';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { PreOrdersService } from '@app/services/pre-orders/pre-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { TableService } from '@app/services/table/table.service';
import { WaitersService } from '@app/services/waiters/waiters.service';
import { Table } from '@app/models/tables/table.model';

const REFRESH_INTERVAL_MS = 15_000;

@Component({
  selector: 'app-kitchen-display',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './kitchen-display.component.html',
  styleUrl: './kitchen-display.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KitchenDisplayComponent implements OnInit {
  private readonly auth = inject(AuthenticationService);
  private readonly reservationService = inject(ReservationService);
  private readonly ordersService = inject(PreOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly tableService = inject(TableService);
  private readonly waiterService = inject(WaitersService);
  private requestInFlight = false;

  readonly reservations = signal<Reservation[]>([]);
  readonly tableNames = signal<Record<string, string>>({});
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly refreshError = signal(false);
  readonly lastUpdated = signal<Date | null>(null);
  readonly updatingReservationIds = signal<Set<string>>(new Set());
  readonly isWaiter = signal(false);

  readonly tickets = computed(() => this.reservations()
    .filter((reservation) =>
      reservation.preOrder != null &&
      reservation.status !== ReservationStatus.Cancelled &&
      reservation.status !== ReservationStatus.Finished &&
      [
        OrderStatus.Submitted,
        OrderStatus.SubmittedByCustomer,
        OrderStatus.Preparing,
        OrderStatus.Ready,
      ].includes(reservation.preOrder.status)
    )
    .sort((left, right) =>
      new Date(left.startTime).getTime() - new Date(right.startTime).getTime()
    ));

  ngOnInit(): void {
    const user = this.auth.getUser();
    this.isWaiter.set(user?.role === 'Waiter');
    if (!this.isWaiter()) {
      this.loading.set(false);
      this.error.set('Kitchen display is available to waiters only.');
      return;
    }

    if (!user?.id) {
      this.loading.set(false);
      this.error.set('Waiter account could not be identified.');
      return;
    }

    this.waiterService.getWaiterLocationId(user.id)
      .pipe(
        switchMap((locationId) => this.tableService.getLocationTables$(locationId)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: (tables) => {
          this.tableNames.set(this.toTableNames(tables));
          this.loadReservations();
          interval(REFRESH_INTERVAL_MS)
            .pipe(takeUntilDestroyed(this.destroyRef))
            .subscribe(() => this.loadReservations());
        },
        error: () => {
          this.loading.set(false);
          this.error.set('The waiter location or its tables could not be loaded.');
        },
      });
  }

  refresh(): void {
    this.loadReservations();
  }

  nextStatus(status: number): OrderStatus | null {
    switch (status) {
      case OrderStatus.Submitted:
      case OrderStatus.SubmittedByCustomer:
        return OrderStatus.Preparing;
      case OrderStatus.Preparing:
        return OrderStatus.Ready;
      case OrderStatus.Ready:
        return OrderStatus.Served;
      default:
        return null;
    }
  }

  statusLabel(status: number): string {
    switch (status) {
      case OrderStatus.Submitted:
      case OrderStatus.SubmittedByCustomer:
        return 'New';
      case OrderStatus.Preparing:
        return 'Preparing';
      case OrderStatus.Ready:
        return 'Ready';
      case OrderStatus.Served:
        return 'Served';
      default:
        return 'Not in kitchen queue';
    }
  }

  actionLabel(status: number): string {
    switch (this.nextStatus(status)) {
      case OrderStatus.Preparing:
        return 'Start preparing';
      case OrderStatus.Ready:
        return 'Mark ready';
      case OrderStatus.Served:
        return 'Mark served';
      default:
        return '';
    }
  }

  isPreparingBlocked(reservation: Reservation): boolean {
    return reservation.preOrder != null &&
      this.nextStatus(reservation.preOrder.status) === OrderStatus.Preparing &&
      reservation.status !== ReservationStatus.InProggress;
  }

  updateStatus(reservation: Reservation): void {
    const order = reservation.preOrder;
    const nextStatus = order && this.nextStatus(order.status);
    if (!order || nextStatus === null || nextStatus === undefined || this.isPreparingBlocked(reservation)) return;
    if (this.updatingReservationIds().has(reservation.id)) return;

    this.updatingReservationIds.update((ids) => new Set(ids).add(reservation.id));
    this.ordersService.updateKitchenStatus(reservation.id, nextStatus)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.reservations.update((items) => items.map((item) =>
            item.id === reservation.id && item.preOrder
              ? { ...item, preOrder: { ...item.preOrder, status: nextStatus } }
              : item
          ));
          this.toast.success(`Order marked ${this.statusLabel(nextStatus).toLowerCase()}.`, 'Kitchen');
          this.finishUpdate(reservation.id);
        },
        error: () => {
          this.toast.error('The kitchen status could not be updated.', 'Kitchen');
          this.finishUpdate(reservation.id);
        },
      });
  }

  reservationStatusLabel(status: number): string {
    return status === ReservationStatus.InProggress ? 'In progress' : 'Waiting for guests';
  }

  private loadReservations(): void {
    if (this.requestInFlight) return;
    this.requestInFlight = true;

    this.reservationService.getReservationsForUser$()
      .pipe(
        switchMap((reservations) =>
          reservations.length === 0
            ? of([])
            : forkJoin(reservations.map((reservation) =>
              this.ordersService.getByReservationId(reservation.id).pipe(
                map((preOrder) => ({ ...reservation, preOrder })),
                catchError((error: unknown) => {
                  if (error instanceof HttpErrorResponse && error.status === 404) {
                    return of(reservation);
                  }
                  return throwError(() => error);
                })
              )
            ))
        ),
        catchError(() => {
          if (this.lastUpdated()) {
            this.refreshError.set(true);
          } else {
            this.error.set('Reservations and kitchen orders could not be loaded.');
          }
          this.requestInFlight = false;
          return of(null);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe((reservations) => {
        if (reservations) {
          this.reservations.set(reservations);
          this.lastUpdated.set(new Date());
          this.refreshError.set(false);
          this.error.set(null);
        }
        this.requestInFlight = false;
        this.loading.set(false);
      });
  }

  private toTableNames(tables: Table[]): Record<string, string> {
    return Object.fromEntries(tables.map((table) => [table.id, `Table ${table.tableNumber}`]));
  }

  private finishUpdate(reservationId: string): void {
    this.updatingReservationIds.update((ids) => {
      const next = new Set(ids);
      next.delete(reservationId);
      return next;
    });
  }
}

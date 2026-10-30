import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { EMPTY, Subject, catchError, exhaustMap, forkJoin, merge, tap, timer } from 'rxjs';
import { GroupReservationStatus, GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';

const SYNC_INTERVAL_MS = 15_000;

@Component({
  selector: 'app-group-order-page',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, ModalComponent, ParticipantCardComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GroupOrderPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly refreshRequests = new Subject<void>();

  readonly reservationId = this.route.snapshot.paramMap.get('reservationId') ?? '';
  readonly invite = signal<GuestInvite | null>(null);
  readonly sessions = signal<GroupSessionsResponse | null>(null);
  readonly ownerSessionId = signal('');
  readonly groupStatus = signal<GroupReservationStatus>('Open');
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly syncError = signal(false);
  readonly updatedAt = signal<Date | null>(null);
  readonly showSubmitDialog = signal(false);
  readonly submitting = signal(false);
  readonly participants = computed(() => this.sessions()?.participants ?? []);
  readonly consolidatedOrder = computed(() => consolidatePreOrder(this.participants()));
  readonly pendingGuestCount = computed(
    () => this.participants().filter((participant) => participant.type === 'Guest' && !participant.isSubmitted).length,
  );
  readonly hasAnyDishes = computed(() => this.participants().some((participant) => participant.items.length > 0));
  readonly statusLabel = computed(() => {
    switch (this.groupStatus()) {
      case 'Open':
        return 'Collecting pre-orders';
      case 'Submitted':
        return 'Sent to Restaurant';
      case 'Closed':
        return 'Closed';
    }
  });

  private pollingStarted = false;
  private pollingSubscription?: { unsubscribe(): void };

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.service.getGroupInvite$(this.reservationId).pipe(
      tap((invite) => this.invite.set(invite)),
      exhaustMap((invite) =>
        forkJoin({
          sessions: this.service.getSessions$(invite.GroupReservationId),
          ownerSessionId: this.service.getOwnerSessionId$(this.reservationId),
          status: this.service.getStatus$(invite.GroupReservationId),
        }),
      ),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: ({ sessions, ownerSessionId, status }) => {
        this.setGroupData(sessions, ownerSessionId, status);
        this.loading.set(false);
        this.updatedAt.set(new Date());
        this.startPolling();
      },
      error: (error: unknown) => {
        this.loading.set(false);
        this.loadError.set(error instanceof HttpErrorResponse && error.status === 404
          ? 'Group reservation not found'
          : 'We couldn’t load the group reservation.');
      },
    });
  }

  refresh(): void {
    this.refreshRequests.next();
  }

  openSubmitDialog(): void {
    if (!this.canSubmit()) return;
    this.showSubmitDialog.set(true);
  }

  closeSubmitDialog(): void {
    if (!this.submitting()) this.showSubmitDialog.set(false);
  }

  canSubmit(): boolean {
    return this.groupStatus() === 'Open' && this.consolidatedOrder().items.length > 0 && !this.submitting();
  }

  submit(): void {
    const groupReservationId = this.invite()?.GroupReservationId;
    if (!groupReservationId || !this.canSubmit()) return;

    this.submitting.set(true);
    this.service.submit$(groupReservationId, this.reservationId).pipe(
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: () => {
        this.submitting.set(false);
        this.showSubmitDialog.set(false);
        this.groupStatus.set('Submitted');
        this.stopPolling();
        this.toast.success('Pre-order sent to the restaurant');
      },
      error: () => this.submitting.set(false),
    });
  }

  private startPolling(): void {
    if (this.pollingStarted || this.groupStatus() !== 'Open') return;
    this.pollingStarted = true;
    this.pollingSubscription = merge(timer(SYNC_INTERVAL_MS, SYNC_INTERVAL_MS), this.refreshRequests).pipe(
      exhaustMap(() => this.synchronize()),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe();
  }

  private synchronize() {
    const groupReservationId = this.invite()?.GroupReservationId;
    if (!groupReservationId || this.groupStatus() !== 'Open') return EMPTY;

    return forkJoin({
      sessions: this.service.getSessions$(groupReservationId, { silent: true }),
      status: this.service.getStatus$(groupReservationId, { silent: true }),
    }).pipe(
      tap(({ sessions, status }) => {
        this.sessions.set(sessions);
        this.groupStatus.set(status.status);
        this.updatedAt.set(new Date());
        this.syncError.set(false);
        if (status.status !== 'Open') this.stopPolling();
      }),
      catchError(() => {
        this.syncError.set(true);
        return EMPTY;
      }),
    );
  }

  private setGroupData(
    sessions: GroupSessionsResponse,
    ownerSessionId: string,
    status: GroupStatusResponse,
  ): void {
    this.sessions.set(sessions);
    this.ownerSessionId.set(ownerSessionId);
    this.groupStatus.set(status.status);
  }

  private stopPolling(): void {
    this.pollingSubscription?.unsubscribe();
    this.pollingStarted = false;
  }
}

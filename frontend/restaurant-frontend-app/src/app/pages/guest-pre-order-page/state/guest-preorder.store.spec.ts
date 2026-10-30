import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { GuestSessionsService } from '@app/services/guest-sessions/guest-sessions.service';
import { GuestPreorderStore } from './guest-preorder.store';

describe('GuestPreorderStore', () => {
    let store: GuestPreorderStore;
    let inviteService: jasmine.SpyObj<GuestInviteService>;
    let sessionsService: jasmine.SpyObj<GuestSessionsService>;

    beforeEach(() => {
        localStorage.clear();
        inviteService = jasmine.createSpyObj<GuestInviteService>('GuestInviteService', ['getInvite$']);
        sessionsService = jasmine.createSpyObj<GuestSessionsService>('GuestSessionsService', [
            'createSession',
            'getMenu',
            'getSession',
            'addDish',
            'removeDish',
            'submit',
        ]);
        inviteService.getInvite$.and.returnValue(of({
            GroupReservationId: 'group-1',
            LocationId: 'location-1',
            RestaurantName: 'Restaurant',
            LocationAddress: 'Address',
            StartTime: '2026-10-17T12:00:00Z',
            EndTime: '2026-10-17T14:00:00Z',
            MaxGuestCount: 2,
            TableNumber: 1,
            InviterName: 'Owner',
        }));
        sessionsService.getMenu.and.returnValue(of({ dishes: {} }));

        TestBed.configureTestingModule({
            providers: [
                GuestPreorderStore,
                { provide: GuestInviteService, useValue: inviteService },
                { provide: GuestSessionsService, useValue: sessionsService },
            ],
        });
        store = TestBed.inject(GuestPreorderStore);
    });

    it('restores an existing guest session instead of creating a duplicate session', async () => {
        localStorage.setItem('guestPreorder:invite-token:guestName', 'Alex');
        localStorage.setItem('guestPreorder:invite-token:guestSessionId', 'session-1');
        localStorage.setItem('guestPreorder:invite-token:guestSessionToken', 'secret');
        sessionsService.getSession.and.returnValue(of({
            name: 'Alex',
            items: [{ dish: { id: 'dish-1', name: 'Soup', price: 12 }, quantity: 2 }],
            total: 24,
            isSubmitted: false,
        }));

        store.initContext('invite-token');
        await new Promise(resolve => setTimeout(resolve, 0));

        expect(sessionsService.getSession).toHaveBeenCalledWith('session-1', 'secret');
        expect(sessionsService.createSession).not.toHaveBeenCalled();
        expect(sessionsService.getMenu).toHaveBeenCalledWith('session-1', 'secret');
        expect(store.cartItems()).toEqual([{
            dishId: 'dish-1',
            title: 'Soup',
            price: 12,
            imageUrl: undefined,
            qty: 2,
        }]);
    });

    it('restores a submitted session without reopening the menu or creating another session', async () => {
        localStorage.setItem('guestPreorder:invite-token:guestName', 'Alex');
        localStorage.setItem('guestPreorder:invite-token:guestSessionId', 'session-1');
        localStorage.setItem('guestPreorder:invite-token:guestSessionToken', 'secret');
        sessionsService.getSession.and.returnValue(of({
            name: 'Alex',
            items: [{ dish: { id: 'dish-1', name: 'Soup', price: 12 }, quantity: 1 }],
            total: 12,
            isSubmitted: true,
        }));

        store.initContext('invite-token');
        await new Promise(resolve => setTimeout(resolve, 0));

        expect(store.status()).toBe('submitted');
        expect(store.cartItems()[0].dishId).toBe('dish-1');
        expect(sessionsService.getMenu).not.toHaveBeenCalled();
        expect(sessionsService.createSession).not.toHaveBeenCalled();
        expect(inviteService.getInvite$).toHaveBeenCalledWith('invite-token');
    });

    it('keeps the name prompt open and shows the capacity error when a new guest cannot join', async () => {
        store.invite.set({
            GroupReservationId: 'group-1',
            LocationId: 'location-1',
            RestaurantName: 'Restaurant',
            LocationAddress: 'Address',
            StartTime: '2026-10-17T12:00:00Z',
            EndTime: '2026-10-17T14:00:00Z',
            MaxGuestCount: 2,
            TableNumber: 1,
            InviterName: 'Owner',
        });
        sessionsService.createSession.and.returnValue(throwError(() => new HttpErrorResponse({
            status: 400,
            error: { message: 'Guest limit exceeded.' },
        })));

        await store.join('Alex');

        expect(store.guestName()).toBe('');
        expect(store.joinError()).toBe('Guest limit exceeded.');
    });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { WaiterDashboardComponent } from './waiter-dashboard.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationService } from '@app/services/reservation/reservation.service';
import { ToastService } from '@app/services/toast/toast.service';
import { HttpErrorResponse } from '@angular/common/http';
import { throwError } from 'rxjs';

describe('WaiterDashboardComponent', () => {
  let component: WaiterDashboardComponent;
  let fixture: ComponentFixture<WaiterDashboardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
      ],
      imports: [WaiterDashboardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(WaiterDashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('renders the Kitchen Display link with router navigation', () => {
    const link = fixture.debugElement.query(By.css('.kitchen-link'));

    expect(link).toBeTruthy();
    expect(link.nativeElement.textContent.trim()).toBe('Kitchen Display');
    expect(link.nativeElement.getAttribute('href')).toBe('/kitchen-display');
  });

  it('shows a clear forbidden message when a waiter selects another restaurant', () => {
    const reservationService = TestBed.inject(ReservationService);
    const toast = TestBed.inject(ToastService);
    spyOn(reservationService, 'makeReservation$').and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 403 }))
    );
    const toastError = spyOn(toast, 'error');

    component.onMakeReservation({
      tableId: 'foreign-table',
      timeRange: {
        start: '2026-10-30T17:00:00.000Z',
        end: '2026-10-30T18:00:00.000Z',
      },
      guests: 2,
      customerEmail: null,
    });

    expect(reservationService.makeReservation$).toHaveBeenCalledWith(
      'foreign-table',
      jasmine.anything(),
      2,
      null,
      true
    );
    expect(toastError).toHaveBeenCalledWith(
      'Вибачте, вам заборонено створювати бронювання в цьому ресторані.',
      'Доступ заборонено'
    );
  });
});

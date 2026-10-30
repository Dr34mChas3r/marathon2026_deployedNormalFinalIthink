import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { CardReservationComponent } from './card-reservation.component';
import { CustomSelectComponent } from '@app/shared/custom-components/custom-select/custom-select.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { ReservationCustomer, ReservationStatusCustomer } from '@app/models/reservations/reservation.model';

const RESERVATION: ReservationCustomer = {
  id: 'res-1',
  tableId: 'table-1',
  startTime: new Date('2026-10-21T16:00:00Z'),
  endTime: new Date('2026-10-21T17:30:00Z'),
  guestCount: 2,
  status: ReservationStatusCustomer.Reserved,
  restaurantName: 'Green & Tasty',
  locationAddress: 'Rustaveli Avenue 48',
  waiterId: 'waiter-1',
};

describe('CardReservationComponent', () => {
  let component: CardReservationComponent;
  let fixture: ComponentFixture<CardReservationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [{ provide: API_BASE_URL, useValue: 'https://test.api' }, provideRouter([])],
      imports: [CardReservationComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(CardReservationComponent);
    component = fixture.componentInstance;
    // r — обов'язковий input: без нього компонент падає з NG0950
    fixture.componentRef.setInput('r', RESERVATION);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('does not offer cancellation after service has started', () => {
    fixture.componentRef.setInput(
      'r',
      { ...RESERVATION, status: ReservationStatusCustomer.InProgress }
    );
    fixture.componentRef.setInput('role', 'Waiter');
    fixture.detectChanges();

    const buttons = fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>;
    expect([...buttons].some((button) => button.textContent?.trim() === 'Cancel')).toBeFalse();
    expect([...buttons].some((button) => button.textContent?.trim() === 'Finish service')).toBeTrue();
  });

  it('reverts the displayed table while a table change is awaiting confirmation', () => {
    fixture.componentRef.setInput('role', 'Waiter');
    fixture.componentRef.setInput('selectedTableLabel', 'Table 3');
    fixture.componentRef.setInput('availableTableOptions', ['Table 5']);
    fixture.componentRef.setInput(
      'availableTableOptionsMap',
      new Map([['Table 5', 'table-5']])
    );
    fixture.detectChanges();

    const tableChanged = spyOn(component.tableChanged, 'emit');
    const select = fixture.debugElement.query(By.directive(CustomSelectComponent))
      .componentInstance as CustomSelectComponent;
    select.selectOption('Table 5');
    fixture.detectChanges();

    expect(select.displayText()).toBe('Table 3');
    expect(tableChanged).toHaveBeenCalledWith({
      reservationId: 'res-1',
      newTableId: 'table-5',
    });
  });
});

import { ComponentFixture, TestBed } from '@angular/core/testing';

import { TimeSlotsModalComponent } from './time-slots-modal.component';

describe('TimeSlotsModalComponent', () => {
  let component: TimeSlotsModalComponent;
  let fixture: ComponentFixture<TimeSlotsModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TimeSlotsModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TimeSlotsModalComponent);
    component = fixture.componentInstance;
    component.locationName = '14 Baratashvili Street';
    component.table = {
      id: '4b5a813c-bf1a-4997-983d-08003194cf9f',
      tableNumber: 3,
      capacity: 4,
      locationId: 'location-id',
      timeRanges: [{ start: '2026-10-30T20:30:00Z', end: '2026-10-30T21:00:00Z' }],
    };
    component.date = '2026-10-30';
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('shows the table number and slot count once without exposing the table ID', () => {
    const subtitle = fixture.nativeElement.querySelector('.subtitle') as HTMLElement;
    const text = subtitle.textContent?.replace(/\s+/g, ' ').trim() ?? '';

    expect(text).toContain('1 slot available at');
    expect(text).toContain('14 Baratashvili Street, Table 3');
    expect(text).not.toContain(component.table.id);
    expect(text.match(/slots? available at/g)?.length).toBe(1);
  });
});

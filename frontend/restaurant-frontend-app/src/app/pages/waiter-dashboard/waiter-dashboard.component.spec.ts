import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { By } from '@angular/platform-browser';
import { WaiterDashboardComponent } from './waiter-dashboard.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

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
});

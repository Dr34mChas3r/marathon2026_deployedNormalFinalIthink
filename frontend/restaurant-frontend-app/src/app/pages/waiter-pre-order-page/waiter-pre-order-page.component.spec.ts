import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { CustomButtonComponent } from '@app/shared/custom-components/custom-button/custom-button.component';
import { WaiterPreOrderPageComponent } from './waiter-pre-order-page.component';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';

describe('WaiterPreOrderPageComponent', () => {
  let component: WaiterPreOrderPageComponent;
  let fixture: ComponentFixture<WaiterPreOrderPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        { provide: API_BASE_URL, useValue: 'https://test.api' },
        provideRouter([]),
      ],
      imports: [WaiterPreOrderPageComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(WaiterPreOrderPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('renders cancel and confirm as separate secondary and primary actions', () => {
    component.createdByRole = 'Customer';
    component.createdByName = 'Customer Doe';
    component.tableName = 'Table 5';
    component.reservationTime = 'Nov 12, 2026, 9:30 AM - 11:00 AM';
    component.orderItems = [{
      dish: {
        id: 'dish-1',
        name: 'Salad',
        imageUrl: '',
        price: 9.99,
        state: 'Available',
        weight: '220g',
        dishType: 'Main course',
        description: '',
        calories: '180',
        proteins: '4g',
        carbohydrates: '20g',
        fats: '8g',
        vitamins: 'A, C, K',
      },
      quantity: 1,
      state: 0,
    }];
    fixture.detectChanges();

    const buttons = fixture.debugElement.queryAll(By.directive(CustomButtonComponent));
    expect(buttons.length).toBe(2);
    expect(buttons[0].componentInstance.variant()).toBe('secondary');
    expect(buttons[1].componentInstance.variant()).toBe('primary');
    expect(buttons.every((button) => button.componentInstance.fullWidth())).toBeTrue();
    expect(fixture.nativeElement.querySelector('h2').textContent).toContain('Table 5');
    expect(fixture.nativeElement.querySelector('h2').textContent).not.toContain('Table Table');
  });
});

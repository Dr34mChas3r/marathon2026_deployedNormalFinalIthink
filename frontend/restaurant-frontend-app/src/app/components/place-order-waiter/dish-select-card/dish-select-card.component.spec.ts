import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DishSelectCardComponent } from './dish-select-card.component';

describe('DishSelectCardComponent', () => {
  let component: DishSelectCardComponent;
  let fixture: ComponentFixture<DishSelectCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DishSelectCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(DishSelectCardComponent);
    component = fixture.componentInstance;
    component.dish = {
      id: 'dish-id',
      name: 'Test dish',
      imageUrl: 'https://invalid.example/dish.jpg',
      price: 10,
      state: 'Available',
      weight: '200g',
      dishType: 'Main',
      description: '',
      calories: '0',
      proteins: '0',
      carbohydrates: '0',
      fats: '0',
      vitamins: '',
    };
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});

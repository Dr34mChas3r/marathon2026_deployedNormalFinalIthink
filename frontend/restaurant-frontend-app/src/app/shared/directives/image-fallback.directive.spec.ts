import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ImageFallbackDirective } from './image-fallback.directive';

@Component({
  standalone: true,
  imports: [ImageFallbackDirective],
  template: '<img src="https://invalid.example/dish.jpg" appImageFallback />',
})
class TestHostComponent {}

describe('ImageFallbackDirective', () => {
  let fixture: ComponentFixture<TestHostComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TestHostComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
    fixture.detectChanges();
  });

  it('replaces a failed dish image with the local fallback', () => {
    const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;

    image.dispatchEvent(new Event('error'));

    expect(image.src).toBe(new URL('assets/img/dish.png', document.baseURI).href);
  });

  it('does not retry the fallback image after it fails', () => {
    const image = fixture.nativeElement.querySelector('img') as HTMLImageElement;
    image.dispatchEvent(new Event('error'));
    const fallbackUrl = image.src;

    image.dispatchEvent(new Event('error'));

    expect(image.src).toBe(fallbackUrl);
  });
});

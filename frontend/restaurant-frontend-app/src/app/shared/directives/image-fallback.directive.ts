import { DOCUMENT } from '@angular/common';
import { Directive, ElementRef, HostListener, inject, Input } from '@angular/core';

@Directive({
  selector: 'img[appImageFallback]',
  standalone: true,
})
export class ImageFallbackDirective {
  @Input() appImageFallback?: string;

  private readonly image = inject(ElementRef<HTMLImageElement>);
  private readonly document = inject(DOCUMENT);
  private failedUrl?: string;

  @HostListener('error')
  onImageError(): void {
    const image = this.image.nativeElement;
    const fallbackUrl = new URL(this.appImageFallback || 'assets/img/dish.png', this.document.baseURI).href;

    if (image.src === fallbackUrl || image.src === this.failedUrl) {
      return;
    }

    this.failedUrl = image.src;
    image.src = fallbackUrl;
  }
}

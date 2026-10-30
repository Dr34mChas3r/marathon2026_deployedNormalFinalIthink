import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { DishListResponse } from '@app/models/dishes/dish-list-response.model';
import { ImageFallbackDirective } from '@app/shared/directives/image-fallback.directive';

@Component({
  selector: 'app-dish-modal',
  standalone: true,
  imports: [ImageFallbackDirective],
  templateUrl: './dish-modal.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './dish-modal.component.scss',
})
export class DishModalComponent {
  @Input() dish!: DishListResponse;
  @Output() closed = new EventEmitter<void>();

  onClose(): void {
    this.closed.emit();
  }
}

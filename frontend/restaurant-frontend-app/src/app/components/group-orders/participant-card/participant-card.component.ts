import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Participant } from '@app/models/group-orders/participant.model';
import { ImageFallbackDirective } from '@app/shared/directives/image-fallback.directive';

@Component({
  selector: 'app-participant-card',
  standalone: true,
  imports: [CurrencyPipe, ImageFallbackDirective],
  templateUrl: './participant-card.component.html',
  styleUrl: './participant-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ParticipantCardComponent {
  readonly participant = input.required<Participant>();
  readonly isMe = input(false);
}

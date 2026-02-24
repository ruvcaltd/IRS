import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-score-indicator',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './score-indicator.component.html',
  styleUrls: ['./score-indicator.component.scss']
})
export class ScoreIndicatorComponent {
  @Input() fundamentalScore: number | null | undefined = null;
  @Input() convictionScore: number | null | undefined = null;
  @Input() score: number | null | undefined = null;

  getDotColor(index: number): string {
    if (this.fundamentalScore == null || this.convictionScore == null) {
      return 'gray';
    }

    const intensity = Math.min(255, Math.abs(this.fundamentalScore) * 50);
    const color = this.fundamentalScore > 0 ? `rgb(0, ${intensity}, 0)` : `rgb(${intensity}, 0, 0)`;

    if (index < this.convictionScore) {
      return color;
    }

    return 'gray';
  }

  get dots(): number[] {
    return [0, 1, 2, 3, 4];
  }
}
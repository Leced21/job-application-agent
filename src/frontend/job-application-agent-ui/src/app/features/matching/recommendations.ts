import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatchingApiService, Recommendation } from './matching-api.service';

@Component({
  selector: 'app-recommendations',
  imports: [DecimalPipe],
  templateUrl: './recommendations.html',
  styleUrl: './recommendations.scss'
})
export class Recommendations {
  private readonly api = inject(MatchingApiService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly items = signal<Recommendation[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = signal(0);
  protected readonly scoringVersion = signal<string | null>(null);
  protected readonly pageSize = 10;

  constructor() { this.load(1); }

  protected load(page: number): void {
    if (this.loading() || page < 1) return;
    this.loading.set(true);
    this.error.set(null);
    this.api.getRecommendations(page, this.pageSize)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.items.set(result.items);
          this.page.set(result.page);
          this.totalCount.set(result.totalCount);
          this.totalPages.set(result.totalPages);
          this.scoringVersion.set(result.scoringVersion ?? null);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(error.status === 404
            ? 'Complétez votre profil pour obtenir des recommandations.'
            : 'Impossible de charger les recommandations. Réessayez dans un instant.');
          this.loading.set(false);
        }
      });
  }
}

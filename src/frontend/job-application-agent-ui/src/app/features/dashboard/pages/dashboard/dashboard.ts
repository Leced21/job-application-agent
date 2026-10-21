import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { MatchingApiService, RecommendationPage } from '../../../matching/matching-api.service';

@Component({
  selector: 'app-dashboard',
  imports: [DecimalPipe, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard {
  private readonly api = inject(MatchingApiService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly page = signal<RecommendationPage | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly totalOffers = computed(() => this.page()?.totalCount ?? 0);
  protected readonly bestRanking = computed(() => {
    const scores = this.page()?.items.map(item => item.rankingScore).filter((score): score is number => score !== null) ?? [];
    return scores.length ? Math.max(...scores) : null;
  });

  constructor() { this.load(); }

  protected load(): void {
    if (this.loading() && this.page() !== null) return;
    this.loading.set(true);
    this.error.set(null);
    this.api.getRecommendations(1, 3).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: page => { this.page.set(page); this.loading.set(false); },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 404
          ? 'Votre profil est nécessaire pour établir votre sélection.'
          : 'Le service Matching ne répond pas pour le moment.');
        this.loading.set(false);
      }
    });
  }
}


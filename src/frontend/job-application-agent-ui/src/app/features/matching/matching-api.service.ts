import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface Recommendation {
  jobId: string;
  title: string;
  companyName: string;
  location: string | null;
  overallScore: number | null;
  rankingScore: number | null;
  evaluationCoverage: number;
  scoringVersion?: string;
  criteria: { name: string; score: number | null; weight: number; status: 0 | 1 | 2; explanation: string }[];
}

export interface RecommendationPage {
  candidateProfileId: string;
  items: Recommendation[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  scoringVersion?: string;
}

@Injectable({ providedIn: 'root' })
export class MatchingApiService {
  private readonly http = inject(HttpClient);

  getRecommendations(page: number, pageSize: number) {
    return this.http.get<RecommendationPage>('/api/v1/matching/recommendations', {
      params: { page, pageSize }
    });
  }
}

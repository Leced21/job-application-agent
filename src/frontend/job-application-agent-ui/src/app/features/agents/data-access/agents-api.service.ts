import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AgentBudget, AgentCatalog, AgentKind, AgentResourceOption, AgentReviewCommand, AgentRun, AgentRunCommand, AgentRunSummary } from '../models/agents.models';

interface AgentMatchingResultsResponse {
  items: { id: string; jobId: string; status: string }[];
}

interface AgentApplicationsResponse {
  items: { id: string; jobTitle: string; companyName: string; status: string }[];
}

@Injectable({ providedIn: 'root' })
export class AgentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/agents';

  catalog(): Observable<AgentCatalog> {
    return this.http.get<AgentCatalog>(`${this.baseUrl}/catalog`);
  }

  budget(): Observable<AgentBudget> {
    return this.http.get<AgentBudget>(`${this.baseUrl}/budget`);
  }

  resources(kind: AgentKind): Observable<AgentResourceOption[]> {
    if (kind === 'ProfileAnalysis') {
      return this.http.get<{ id: string; firstName: string; lastName: string }>('/api/v1/profile').pipe(
        map(profile => [{ id: profile.id, label: `${profile.firstName} ${profile.lastName}`.trim() || 'Mon profil' }])
      );
    }

    if (kind === 'MatchingEnrichment') {
      return this.http.get<AgentMatchingResultsResponse>('/api/v1/matching/results?page=1&pageSize=50&status=Completed').pipe(
        map(response => response.items.map(result => ({
          id: result.id,
          label: `Résultat de matching · offre ${result.jobId.slice(0, 8)}`
        })))
      );
    }

    if (kind === 'FollowUpSuggestion') {
      return this.http.get<AgentApplicationsResponse>('/api/v1/applications?page=1&pageSize=20').pipe(
        map(response => response.items
          .filter(application => ['Submitted', 'InReview', 'Interview'].includes(application.status))
          .map(application => ({ id: application.id, label: `${application.jobTitle} · ${application.companyName}` })))
      );
    }

    return this.http.get<{ id: string; title: string; companyName: string; status: string }[]>('/api/v1/jobs?page=1&pageSize=100').pipe(
      map(jobs => jobs
        .filter(job => job.status === 'Discovered' || job.status === 'Saved')
        .map(job => ({ id: job.id, label: `${job.title} · ${job.companyName}` })))
    );
  }

  execute(command: AgentRunCommand): Observable<AgentRun> {
    return this.http.post<AgentRun>(`${this.baseUrl}/runs`, command);
  }

  listRuns(): Observable<AgentRunSummary[]> {
    return this.http.get<AgentRunSummary[]>(`${this.baseUrl}/runs?page=1&pageSize=50`);
  }

  getRun(id: string): Observable<AgentRun> {
    return this.http.get<AgentRun>(`${this.baseUrl}/runs/${encodeURIComponent(id)}`);
  }

  review(id: string, command: AgentReviewCommand): Observable<AgentRun> {
    return this.http.post<AgentRun>(`${this.baseUrl}/runs/${encodeURIComponent(id)}/review`, command);
  }
}

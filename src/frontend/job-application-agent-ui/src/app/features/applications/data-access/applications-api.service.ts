import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApplicationDetail,
  ApplicationPage,
  ApplicationStatus,
  ContactChannel,
  DueFollowUpPage,
  SubmissionAttemptOutcome,
  SubmissionPlatformOptions,
  SubmissionPreview,
} from '../models/application.models';

@Injectable({ providedIn: 'root' })
export class ApplicationsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/applications';

  list(status: ApplicationStatus | null): Observable<ApplicationPage> {
    let params = new HttpParams().set('page', 1).set('pageSize', 100);
    if (status) params = params.set('status', status);
    return this.http.get<ApplicationPage>(this.baseUrl, { params });
  }

  create(requestId: string, profileId: string, jobId: string): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, { requestId, profileId, jobId });
  }

  attachDocument(id: string, revision: number, exportId: string): Observable<{ revision: number }> {
    return this.http.post<{ revision: number }>(
      `${this.baseUrl}/${encodeURIComponent(id)}/documents`,
      {
        expectedRevision: revision,
        exportId,
      },
    );
  }

  requestReview(id: string, revision: number): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(`${this.baseUrl}/${encodeURIComponent(id)}/review`, {
      expectedRevision: revision,
    });
  }

  approve(id: string, revision: number, confirmed: true): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(`${this.baseUrl}/${encodeURIComponent(id)}/approve`, {
      expectedRevision: revision,
      confirmed,
    });
  }

  returnToDraft(id: string, revision: number): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/return-to-draft`,
      { expectedRevision: revision, confirmed: true },
    );
  }

  withdraw(id: string, revision: number, reason: string): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(`${this.baseUrl}/${encodeURIComponent(id)}/withdraw`, {
      expectedRevision: revision,
      confirmed: true,
      reason,
    });
  }

  dueFollowUps(): Observable<DueFollowUpPage> {
    return this.http.get<DueFollowUpPage>(`${this.baseUrl}/follow-ups/due?page=1&pageSize=100`);
  }

  get(id: string): Observable<ApplicationDetail> {
    return this.http.get<ApplicationDetail>(`${this.baseUrl}/${encodeURIComponent(id)}`);
  }

  submissionPlatforms(): Observable<SubmissionPlatformOptions> {
    return this.http.get<SubmissionPlatformOptions>(`${this.baseUrl}/submission-platforms`);
  }

  submissionPreview(id: string): Observable<SubmissionPreview> {
    return this.http.get<SubmissionPreview>(
      `${this.baseUrl}/${encodeURIComponent(id)}/submission-preview`,
    );
  }

  approveControlledSubmission(
    id: string,
    revision: number,
    reviewSha256: string,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/submission-approval`,
      { expectedRevision: revision, confirmed: true, reviewSha256 },
    );
  }

  prepareSubmissionAttempt(
    id: string,
    revision: number,
    attemptId: string,
    platformId: string,
    reviewSha256: string,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/submission-attempts`,
      { expectedRevision: revision, confirmed: true, attemptId, platformId, reviewSha256 },
    );
  }

  recordSubmissionAttemptResult(
    id: string,
    revision: number,
    attemptId: string,
    outcome: SubmissionAttemptOutcome,
    occurredAtUtc: string,
    reference: string | null,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/submission-attempt-result`,
      { expectedRevision: revision, attemptId, outcome, occurredAtUtc, confirmed: true, reference },
    );
  }

  addNote(id: string, revision: number, note: string): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(`${this.baseUrl}/${encodeURIComponent(id)}/notes`, {
      expectedRevision: revision,
      note,
    });
  }

  declareManualSubmission(
    id: string,
    revision: number,
    occurredAtUtc: string,
    channel: ContactChannel,
    reference: string | null,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/manual-submission`,
      {
        expectedRevision: revision,
        confirmed: true,
        occurredAtUtc,
        channel,
        reference,
      },
    );
  }

  recordRecruiterUpdate(
    id: string,
    revision: number,
    status: ApplicationStatus,
    occurredAtUtc: string,
    channel: ContactChannel,
    message: string,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/recruiter-updates`,
      { expectedRevision: revision, confirmed: true, status, occurredAtUtc, channel, message },
    );
  }

  planFollowUp(
    id: string,
    revision: number,
    dueAtUtc: string,
    channel: ContactChannel,
    note: string,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/follow-ups`,
      { expectedRevision: revision, dueAtUtc, channel, note },
    );
  }

  rescheduleFollowUp(
    id: string,
    followUpId: string,
    revision: number,
    dueAtUtc: string,
    channel: ContactChannel,
    note: string,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/follow-ups/${encodeURIComponent(followUpId)}/reschedule`,
      { expectedRevision: revision, dueAtUtc, channel, note },
    );
  }

  completeFollowUp(
    id: string,
    followUpId: string,
    revision: number,
    occurredAtUtc: string,
    channel: ContactChannel,
    outcome: string,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/follow-ups/${encodeURIComponent(followUpId)}/complete`,
      { expectedRevision: revision, confirmed: true, occurredAtUtc, channel, outcome },
    );
  }

  cancelFollowUp(
    id: string,
    followUpId: string,
    revision: number,
    reason: string,
  ): Observable<ApplicationDetail> {
    return this.http.post<ApplicationDetail>(
      `${this.baseUrl}/${encodeURIComponent(id)}/follow-ups/${encodeURIComponent(followUpId)}/cancel`,
      { expectedRevision: revision, confirmed: true, reason },
    );
  }
}

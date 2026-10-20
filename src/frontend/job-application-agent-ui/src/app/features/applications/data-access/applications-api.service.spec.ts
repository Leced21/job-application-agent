import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationsApiService } from './applications-api.service';

describe('ApplicationsApiService controlled submission contracts', () => {
  let service: ApplicationsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ApplicationsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the authenticated gateway contracts for discovery, review, reservation, and result', () => {
    const id = 'application-123';
    const reviewSha256 = 'a'.repeat(64);
    const attemptId = 'attempt-123';

    service.submissionPlatforms().subscribe();
    service.submissionPreview(id).subscribe();
    service.approveControlledSubmission(id, 7, reviewSha256).subscribe();
    service.prepareSubmissionAttempt(id, 8, attemptId, 'official-api', reviewSha256).subscribe();
    service
      .recordSubmissionAttemptResult(id, 9, attemptId, 'Unknown', '2026-10-18T10:00:00.000Z', null)
      .subscribe();

    const platforms = http.expectOne('/api/v1/applications/submission-platforms');
    const preview = http.expectOne('/api/v1/applications/application-123/submission-preview');
    const approval = http.expectOne('/api/v1/applications/application-123/submission-approval');
    const reservation = http.expectOne('/api/v1/applications/application-123/submission-attempts');
    const result = http.expectOne('/api/v1/applications/application-123/submission-attempt-result');
    expect(platforms.request.method).toBe('GET');
    expect(preview.request.method).toBe('GET');
    expect(approval.request.body).toEqual({ expectedRevision: 7, confirmed: true, reviewSha256 });
    expect(reservation.request.body).toEqual({
      expectedRevision: 8,
      confirmed: true,
      attemptId,
      platformId: 'official-api',
      reviewSha256,
    });
    expect(result.request.body).toEqual({
      expectedRevision: 9,
      attemptId,
      outcome: 'Unknown',
      occurredAtUtc: '2026-10-18T10:00:00.000Z',
      confirmed: true,
      reference: null,
    });
    for (const request of [platforms, preview, approval, reservation, result]) {
      expect(request.request.headers.has('Authorization')).toBe(false);
      request.flush({});
    }
  });
});

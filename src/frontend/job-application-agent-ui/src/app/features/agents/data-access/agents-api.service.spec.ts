import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AgentsApiService } from './agents-api.service';

describe('AgentsApiService', () => {
  let service: AgentsApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AgentsApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reads the catalog and budget through gateway routes without browser bearer tokens', () => {
    service.catalog().subscribe();
    service.budget().subscribe();

    const catalog = http.expectOne('/api/v1/agents/catalog');
    const budget = http.expectOne('/api/v1/agents/budget');
    expect(catalog.request.method).toBe('GET');
    expect(budget.request.method).toBe('GET');
    expect(catalog.request.headers.has('Authorization')).toBe(false);
    expect(budget.request.headers.has('Authorization')).toBe(false);
    catalog.flush({ definitions: [] });
    budget.flush({ maxTokens: 0, maxUsd: 0, chargedTokens: 0, chargedUsd: 0, heldTokens: 0, heldUsd: 0 });
  });

  it('loads owned profile choices and submits agent runs through the gateway session', () => {
    let choices: { id: string; label: string }[] = [];
    service.resources('ProfileAnalysis').subscribe(value => choices = value);
    const profile = http.expectOne('/api/v1/profile');
    expect(profile.request.method).toBe('GET');
    profile.flush({ id: 'profile-1', firstName: 'Cédric', lastName: 'Martin' });
    expect(choices).toEqual([{ id: 'profile-1', label: 'Cédric Martin' }]);

    const command = { requestId: 'request-1', kind: 'ProfileAnalysis' as const, resourceId: 'profile-1' };
    service.execute(command).subscribe();
    const run = http.expectOne('/api/v1/agents/runs');
    expect(run.request.method).toBe('POST');
    expect(run.request.body).toEqual(command);
    expect(run.request.headers.has('Authorization')).toBe(false);
    run.flush({ id: 'run-1', command, state: 'PendingReview', revision: 2 });
  });

  it('loads run history and reviews the exact pending proposal revision', () => {
    service.listRuns().subscribe();
    const history = http.expectOne('/api/v1/agents/runs?page=1&pageSize=50');
    expect(history.request.method).toBe('GET');
    history.flush([]);

    const review = {
      expectedRevision: 2,
      outputSha256: 'abc123',
      confirmed: true as const,
      approved: true,
      note: null,
    };
    service.review('run-1', review).subscribe();
    const request = http.expectOne('/api/v1/agents/runs/run-1/review');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(review);
    request.flush({ id: 'run-1', state: 'Approved', revision: 3 });
  });

});

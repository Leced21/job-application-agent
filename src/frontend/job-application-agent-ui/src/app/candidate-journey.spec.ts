import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { csrfInterceptor } from './core/auth/csrf.interceptor';
import { AccountSession } from './core/auth/auth-api.service';
import { RecommendationPage } from './features/matching/matching-api.service';
import { routes } from './app.routes';

describe('candidate journey', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  let router: Router;

  const anonymousSession: AccountSession = {
    isAuthenticated: false,
    email: null,
    registrationEnabled: false,
  };

  const candidateSession: AccountSession = {
    isAuthenticated: true,
    email: 'candidate@example.test',
    registrationEnabled: false,
  };

  const recommendations: RecommendationPage = {
    candidateProfileId: 'profile-1',
    page: 1,
    pageSize: 3,
    totalCount: 1,
    totalPages: 1,
    scoringVersion: 'matching-v2:rules-1',
    items: [
      {
        jobId: 'job-1',
        title: 'Développeuse .NET',
        companyName: 'Atelier Numérique',
        location: 'Lyon',
        overallScore: 82,
        rankingScore: 78,
        evaluationCoverage: 90,
        criteria: [],
      },
    ],
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        provideHttpClient(withInterceptors([csrfInterceptor])),
        provideHttpClientTesting(),
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    harness = await RouterTestingHarness.create('/login');
  });

  afterEach(() => http.verify());

  it('redirects an anonymous candidate to sign-in, then loads and opens recommendations', async () => {
    const initialNavigation = router.navigateByUrl('/dashboard');
    await new Promise(resolve => setTimeout(resolve, 0));
    const anonymousChecks = http.match('/auth/session');
    expect(anonymousChecks).toHaveLength(2);
    anonymousChecks.forEach(request => request.flush(anonymousSession));
    await initialNavigation;
    harness.detectChanges();

    const email = harness.routeNativeElement?.querySelector<HTMLInputElement>('#email');
    const password = harness.routeNativeElement?.querySelector<HTMLInputElement>('#password');
    expect(email).not.toBeNull();
    expect(password).not.toBeNull();

    email!.value = 'candidate@example.test';
    email!.dispatchEvent(new Event('input'));
    password!.value = 'a-valid-password';
    password!.dispatchEvent(new Event('input'));
    harness.detectChanges();
    harness.routeNativeElement?.querySelector<HTMLButtonElement>('button[type="submit"]')?.click();

    http.expectOne('/auth/antiforgery').flush({ requestToken: 'csrf-token' });
    http
      .expectOne((request) => request.url === '/auth/login' && request.method === 'POST')
      .flush(candidateSession);
    await new Promise(resolve => setTimeout(resolve, 0));
    http.expectOne('/auth/session').flush(candidateSession);
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(router.url).toBe('/dashboard');
    http
      .expectOne(
        (request) =>
          request.url === '/api/v1/matching/recommendations' &&
          request.params.get('page') === '1' &&
          request.params.get('pageSize') === '3',
      )
      .flush(recommendations);
    harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain(
      'Faites avancer votre prochaine étape.',
    );
    expect(harness.routeNativeElement?.textContent).toContain('Atelier Numérique');

    harness.routeNativeElement
      ?.querySelector<HTMLAnchorElement>('a[href="/recommendations"]')
      ?.click();
    http.expectOne('/auth/session').flush(candidateSession);
    await harness.fixture.whenStable();
    http
      .expectOne(
        (request) =>
          request.url === '/api/v1/matching/recommendations' &&
          request.params.get('page') === '1' &&
          request.params.get('pageSize') === '10',
      )
      .flush(recommendations);
    harness.detectChanges();

    expect(router.url).toBe('/recommendations');
    expect(harness.routeNativeElement?.textContent).toContain('Des offres qui vous correspondent.');
    expect(harness.routeNativeElement?.textContent).toContain('Développeuse .NET');
  });
});

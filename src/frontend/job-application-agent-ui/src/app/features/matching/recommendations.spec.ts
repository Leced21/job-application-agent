import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Recommendations } from './recommendations';

describe('Recommendations', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [Recommendations],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('renders scores and requests the next ranked page', () => {
    const fixture = TestBed.createComponent(Recommendations);
    http.expectOne('/api/v1/matching/recommendations?page=1&pageSize=10').flush({
      candidateProfileId: 'profile', page: 1, pageSize: 10, totalCount: 20, totalPages: 2,
      items: Array.from({ length: 10 }, (_, index) => ({
        jobId: `job-${index}`, title: 'Data Engineer', companyName: 'Example', location: 'Paris',
        overallScore: 100, rankingScore: 50, evaluationCoverage: 50,
        criteria: [{ name: 'Skills', score: null, weight: .5, explanation: 'Données manquantes' }]
      }))
    });
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('50 / 100');
    expect(element.textContent).toContain('Non évalué');
    expect(element.textContent).toContain('Données manquantes');
    const next = Array.from(element.querySelectorAll('button')).find(button => button.textContent?.includes('Suivant'))!;
    next.click();
    http.expectOne('/api/v1/matching/recommendations?page=2&pageSize=10')
      .flush({ candidateProfileId: 'profile', page: 2, pageSize: 10, items: [], totalCount: 10, totalPages: 1 });
    fixture.detectChanges();
    expect(element.textContent).toContain('fin des recommandations');
    expect(next.disabled).toBe(true);
  });

  it('disables next on a full final page using server totals', () => {
    const fixture = TestBed.createComponent(Recommendations);
    http.expectOne('/api/v1/matching/recommendations?page=1&pageSize=10').flush({
      candidateProfileId: 'profile', page: 1, pageSize: 10, totalCount: 10, totalPages: 1,
      items: Array.from({ length: 10 }, (_, index) => ({
        jobId: `job-${index}`, title: 'Engineer', companyName: 'Example', location: null,
        overallScore: null, rankingScore: null, evaluationCoverage: 0, criteria: []
      }))
    });
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('10 offres');
    expect(element.textContent).toContain('Page 1 sur 1');
    expect(Array.from(element.querySelectorAll('button')).find(button => button.textContent?.includes('Suivant'))!.disabled).toBe(true);
  });

  it('distinguishes missing and not applicable criteria and displays the rule version', () => {
    const fixture = TestBed.createComponent(Recommendations);
    http.expectOne('/api/v1/matching/recommendations?page=1&pageSize=10').flush({
      candidateProfileId: 'profile', page: 1, pageSize: 10, totalCount: 1, totalPages: 1,
      scoringVersion: 'deterministic-v2:test-fingerprint',
      items: [{
        jobId: 'job-1', title: 'Engineer', companyName: 'Example', location: null,
        overallScore: null, rankingScore: null, evaluationCoverage: 0,
        criteria: [
          { name: 'Skills', score: null, weight: 0, status: 2, explanation: 'Criterion disabled' },
          { name: 'Location', score: null, weight: .2, status: 1, explanation: 'Preferences missing' }
        ]
      }]
    });
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.textContent).toContain('Version des règles : deterministic-v2');
    expect(element.textContent).toContain('Skills Non applicable');
    expect(element.textContent).toContain('Location Non évalué');
    expect(element.textContent).toContain('Criterion disabled');
    expect(element.querySelector('[title="deterministic-v2:test-fingerprint"]')).not.toBeNull();
  });

  it('explains a missing profile and allows retry', () => {
    const fixture = TestBed.createComponent(Recommendations);
    http.expectOne('/api/v1/matching/recommendations?page=1&pageSize=10')
      .flush({}, { status: 404, statusText: 'Not Found' });
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Complétez votre profil');
    Array.from(element.querySelectorAll('button')).find(button => button.textContent === 'Réessayer')!.click();
    http.expectOne('/api/v1/matching/recommendations?page=1&pageSize=10')
      .flush({ candidateProfileId: 'profile', page: 1, pageSize: 10, items: [], totalCount: 0, totalPages: 0 });
    fixture.detectChanges();
    expect(element.textContent).toContain('Aucune offre disponible');
  });
});

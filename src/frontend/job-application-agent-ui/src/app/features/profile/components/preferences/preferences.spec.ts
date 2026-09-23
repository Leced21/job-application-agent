import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Preferences } from './preferences';
import { CandidatePreferences } from '../../models/preferences.model';

describe('Preferences', () => {
  let fixture: ComponentFixture<Preferences>;
  let component: Preferences;
  let http: HttpTestingController;
  const route = '/api/v1/profile/preferences';
  const value: CandidatePreferences = {
    candidateProfileId: '1', desiredJobTitles: ['Developer'], preferredLocations: ['Paris'],
    contractTypes: ['CDI'], workModes: ['Hybrid'], minimumAnnualGrossSalary: 50000,
    salaryCurrency: 'EUR', availableFrom: '2027-01-01',
    createdAtUtc: '2025-01-01T00:00:00Z', updatedAtUtc: '2025-01-01T00:00:00Z'
  };
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Preferences], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Preferences); component = fixture.componentInstance;
  });
  afterEach(() => { http.verify(); fixture.destroy(); });
  function load() { http.expectOne(route).flush(value); fixture.detectChanges(); }
  function missing() { http.expectOne(route).flush({}, {status: 404, statusText: 'Not Found'}); fixture.detectChanges(); }

  it('treats 404 as absent preferences and allows first save', () => {
    missing();
    expect(fixture.nativeElement.textContent).toContain('Aucune préférence');
    component['startEditing']();
    component['savePreferences']();
    const request = http.expectOne(route);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      desiredJobTitles: [], preferredLocations: [], contractTypes: [], workModes: [],
      minimumAnnualGrossSalary: null, salaryCurrency: null, availableFrom: null
    });
    request.flush({...value, ...request.request.body});
    expect(component['editing']()).toBe(false);
  });

  it('retries server errors instead of treating them as empty', () => {
    http.expectOne(route).flush({}, { status: 500, statusText: 'Error' }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Impossible de charger');
    component['loadPreferences'](); load();
    expect(fixture.nativeElement.textContent).toContain('Hybride');
  });

  it('prefills and replaces lists, trims entries and normalizes currency with zero salary', () => {
    load(); component['startEditing']();
    expect(component['form'].controls.desiredJobTitles.value).toBe('Developer');
    expect(component['form'].controls.hybrid.value).toBe(true);
    component['form'].patchValue({
      desiredJobTitles: [' Architect ', '', ' Developer '].join(String.fromCharCode(10)), preferredLocations: '',
      contractTypes: ' Freelance ', hybrid: false, remote: true,
      minimumAnnualGrossSalary: 0, salaryCurrency: 'eur'
    });
    component['savePreferences'](); component['savePreferences']();
    const request = http.expectOne(route);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.desiredJobTitles).toEqual(['Architect', 'Developer']);
    expect(request.request.body.preferredLocations).toEqual([]);
    expect(request.request.body.workModes).toEqual(['Remote']);
    expect(request.request.body.minimumAnnualGrossSalary).toBe(0);
    expect(request.request.body.salaryCurrency).toBe('EUR');
    request.flush({...value, ...request.request.body});
  });

  it('clears currency when salary is cleared and keeps input after save failure', () => {
    load(); component['startEditing']();
    component['form'].controls.minimumAnnualGrossSalary.setValue(null);
    component['savePreferences']();
    const request = http.expectOne(route);
    expect(request.request.body.salaryCurrency).toBeNull();
    request.flush({}, {status: 500, statusText: 'Error'});
    expect(component['editing']()).toBe(true);
    expect(component['form'].controls.desiredJobTitles.value).toBe('Developer');
    expect(component['saveError']()).toBeTruthy();
    component['cancelForm']();
    expect(component['preferences']()?.minimumAnnualGrossSalary).toBe(50000);
  });

  it.each([-1, 12.345, 10000000000])('rejects invalid salary %s', salary => {
    load(); component['startEditing']();
    component['form'].controls.minimumAnnualGrossSalary.setValue(salary);
    component['savePreferences'](); http.expectNone(route);
    expect(component['form'].invalid).toBe(true);
  });

  it('requires a currency for salary and enforces list limits', () => {
    load(); component['startEditing']();
    component['form'].controls.salaryCurrency.setValue('');
    component['savePreferences'](); http.expectNone(route);
    component['form'].controls.salaryCurrency.setValue('EUR');
    component['form'].controls.desiredJobTitles.setValue(Array(21).fill('Developer').join(String.fromCharCode(10)));
    expect(component['form'].controls.desiredJobTitles.invalid).toBe(true);
    component['form'].controls.preferredLocations.setValue('x'.repeat(201));
    component['savePreferences'](); http.expectNone(route);
    expect(component['form'].controls.preferredLocations.invalid).toBe(true);
  });

  it('confirms deletion and retains preferences on failure before retry', () => {
    load(); component['deletePreferences'](); http.expectNone(route);
    component['requestDelete'](); component['cancelDelete']();
    expect(component['preferences']()).toBeTruthy();
    component['requestDelete'](); component['deletePreferences'](); component['deletePreferences']();
    const request = http.expectOne(route);
    expect(request.request.method).toBe('DELETE');
    request.flush({}, {status: 500, statusText: 'Error'});
    expect(component['preferences']()).toBeTruthy();
    component['deletePreferences'](); http.expectOne(route).flush(null);
    expect(component['preferences']()).toBeNull();
  });
});

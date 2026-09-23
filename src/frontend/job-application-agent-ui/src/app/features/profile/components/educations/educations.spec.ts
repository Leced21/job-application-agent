import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Educations } from './educations';
import { Education } from '../../models/education.model';

describe('Educations', () => {
  let fixture: ComponentFixture<Educations>;
  let component: Educations;
  let http: HttpTestingController;
  const route = '/api/v1/profile/educations';
  const item: Education = {
    id: 'education-1', institutionName: 'Université de Paris', degree: 'Master',
    fieldOfStudy: 'Informatique', location: 'Paris', startDate: '2023-09-01',
    endDate: '2025-06-30', isCurrent: false, description: 'Formation informatique',
    createdAtUtc: '2025-01-01T00:00:00Z', updatedAtUtc: '2025-01-01T00:00:00Z'
  };
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Educations], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Educations);
    component = fixture.componentInstance;
  });
  afterEach(() => { http.verify(); fixture.destroy(); });
  function load(items: Education[] = []) {
    http.expectOne(route).flush(items);
    fixture.detectChanges();
  }
  function fill() {
    component['startAdding']();
    component['form'].patchValue({ institutionName: ' Université ', degree: ' Master ', startDate: '2023-09-01' });
  }

  it('retries loading and displays the empty state', () => {
    http.expectOne(route).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Impossible de charger');
    component['loadEducations']();
    load();
    expect(fixture.nativeElement.textContent).toContain('Aucune formation');
  });

  it('creates a current education with a null end date and blocks duplicate submissions', () => {
    load(); fill();
    component['form'].controls.endDate.setValue('2025-06-30');
    component['form'].controls.isCurrent.setValue(true);
    expect(component['form'].controls.endDate.disabled).toBe(true);
    expect(component['form'].controls.endDate.value).toBe('');
    component['saveEducation']();
    component['saveEducation']();
    const request = http.expectOne(route);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      institutionName: 'Université', degree: 'Master', startDate: '2023-09-01',
      endDate: null, isCurrent: true, fieldOfStudy: null, location: null, description: null
    });
    request.flush({ ...item, isCurrent: true, endDate: null });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('En cours');
    expect(component['adding']()).toBe(false);
    component['startAdding']();
    expect(component['form'].controls.endDate.enabled).toBe(true);
  });

  it('rejects blank required fields, excessive lengths and reversed dates', () => {
    load(); fill();
    component['form'].controls.degree.setValue('  ');
    component['saveEducation'](); http.expectNone(route);
    component['form'].patchValue({ degree: 'Master', description: 'a'.repeat(4001) });
    component['saveEducation'](); http.expectNone(route);
    component['form'].patchValue({ description: '', endDate: '2020-01-01' });
    component['saveEducation'](); http.expectNone(route);
    expect(component['form'].hasError('dateOrder')).toBe(true);
  });

  it('prefills all fields, retains input after failure and updates in place', () => {
    load([item]); component['startEditing'](item);
    expect(component['form'].getRawValue()).toEqual({
      institutionName: item.institutionName, degree: item.degree, fieldOfStudy: item.fieldOfStudy,
      location: item.location, startDate: item.startDate, endDate: item.endDate,
      isCurrent: false, description: item.description
    });
    component['form'].controls.degree.setValue('Doctorat');
    component['saveEducation']();
    const request = http.expectOne(route + '/education-1');
    expect(request.request.method).toBe('PUT');
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(component['form'].controls.degree.value).toBe('Doctorat');
    expect(component['saveError']()).toBeTruthy();
    component['saveEducation']();
    http.expectOne(route + '/education-1').flush({ ...item, degree: 'Doctorat' });
    expect(component['educations']().length).toBe(1);
    expect(component['educations']()[0].degree).toBe('Doctorat');
  });

  it('requires deletion confirmation and allows retry after failure', () => {
    load([item]);
    component['deleteEducation'](item.id); http.expectNone(route + '/education-1');
    component['requestDelete'](item.id); component['cancelDelete']();
    expect(component['educations']().length).toBe(1);
    component['requestDelete'](item.id); component['deleteEducation'](item.id);
    component['deleteEducation'](item.id);
    const request = http.expectOne(route + '/education-1');
    expect(request.request.method).toBe('DELETE');
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(component['educations']().length).toBe(1);
    component['deleteEducation'](item.id);
    http.expectOne(route + '/education-1').flush(null);
    expect(component['educations']()).toEqual([]);
  });

  it('sorts by most recent start date and restores end date editing', () => {
    load([item, { ...item, id: '2', startDate: '2026-01-01', isCurrent: true, endDate: null }]);
    expect(component['educations']()[0].id).toBe('2');
    component['startEditing'](component['educations']()[0]);
    expect(component['form'].controls.endDate.disabled).toBe(true);
    component['form'].controls.isCurrent.setValue(false);
    expect(component['form'].controls.endDate.enabled).toBe(true);
    component['cancelForm']();
    expect(component['editingId']()).toBeNull();
  });
});

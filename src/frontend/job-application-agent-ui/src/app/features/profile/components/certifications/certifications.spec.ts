import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Certifications } from './certifications';
import { Certification } from '../../models/certification.model';

describe('Certifications', () => {
  let fixture: ComponentFixture<Certifications>;
  let component: Certifications;
  let http: HttpTestingController;
  const route = '/api/v1/profile/certifications';
  const item: Certification = {
    id: 'cert-1', name: 'Azure', issuingOrganization: 'Microsoft', issueDate: '2025-01-01',
    expirationDate: null, credentialId: null, credentialUrl: 'https://example.com/certificate',
    createdAtUtc: '2025-01-01T00:00:00Z', updatedAtUtc: '2025-01-01T00:00:00Z'
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Certifications], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Certifications);
    component = fixture.componentInstance;
  });
  afterEach(() => { http.verify(); fixture.destroy(); });

  function load(items: Certification[] = []) {
    http.expectOne(route).flush(items);
    fixture.detectChanges();
  }
  function fill() {
    component['startAdding']();
    component['form'].patchValue({ name: ' Azure ', issuingOrganization: ' Microsoft ', issueDate: '2025-01-01' });
  }

  it('renders an empty state and retries a failed load', () => {
    http.expectOne(route).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Impossible de charger');
    component['loadCertifications']();
    load();
    expect(fixture.nativeElement.textContent).toContain('Aucune certification');
  });

  it('creates using trimmed values and null optional fields without duplicate requests', () => {
    load(); fill();
    component['saveCertification']();
    component['saveCertification']();
    const request = http.expectOne(route);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      name: 'Azure', issuingOrganization: 'Microsoft', issueDate: '2025-01-01',
      expirationDate: null, credentialId: null, credentialUrl: null
    });
    request.flush(item);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Azure');
    expect(component['adding']()).toBe(false);
  });

  it('rejects whitespace, invalid URLs and reversed dates', () => {
    load(); fill();
    component['form'].patchValue({ name: '   ' });
    component['saveCertification'](); http.expectNone(route);
    component['form'].patchValue({ name: 'Azure', credentialUrl: 'javascript:alert(1)' });
    component['saveCertification'](); http.expectNone(route);
    component['form'].patchValue({ credentialUrl: '', expirationDate: '2024-01-01' });
    component['saveCertification'](); http.expectNone(route);
    expect(component['form'].hasError('dateOrder')).toBe(true);
  });

  it('prefills, preserves values on error and updates the existing item on retry', () => {
    load([item]);
    component['startEditing'](item);
    expect(component['form'].controls.expirationDate.value).toBe('');
    component['form'].patchValue({ name: 'Updated', credentialUrl: '' });
    component['saveCertification']();
    const failed = http.expectOne(route + '/cert-1');
    expect(failed.request.method).toBe('PUT');
    failed.flush({}, { status: 500, statusText: 'Error' });
    expect(component['form'].controls.name.value).toBe('Updated');
    expect(component['saveError']()).toBeTruthy();
    component['saveCertification']();
    http.expectOne(route + '/cert-1').flush({ ...item, name: 'Updated', credentialUrl: null });
    expect(component['certifications']().length).toBe(1);
    expect(component['certifications']()[0].name).toBe('Updated');
  });

  it('requires confirmation and keeps the item after deletion failure', () => {
    load([item]);
    component['deleteCertification'](item.id); http.expectNone(route + '/cert-1');
    component['requestDelete'](item.id);
    component['cancelDelete']();
    expect(component['certifications']().length).toBe(1);
    component['requestDelete'](item.id);
    component['deleteCertification'](item.id);
    component['deleteCertification'](item.id);
    const failed = http.expectOne(route + '/cert-1');
    expect(failed.request.method).toBe('DELETE');
    failed.flush({}, { status: 500, statusText: 'Error' });
    expect(component['certifications']().length).toBe(1);
    expect(component['deleteError']()).toBeTruthy();
    component['deleteCertification'](item.id);
    http.expectOne(route + '/cert-1').flush(null);
    expect(component['certifications']()).toEqual([]);
  });

  it('sorts by name and renders only HTTP certificate links', () => {
    load([{ ...item, id: '2', name: 'Zebra', credentialUrl: 'javascript:alert(1)' }, item]);
    expect(component['certifications']()[0].name).toBe('Azure');
    const anchors = fixture.nativeElement.querySelectorAll('a');
    expect(anchors.length).toBe(1);
    expect(anchors[0].getAttribute('rel')).toBe('noopener noreferrer');
    expect(anchors[0].getAttribute('target')).toBe('_blank');
  });
});

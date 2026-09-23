import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Links } from './links';
import { Link } from '../../models/link.model';

describe('Links', () => {
  let fixture: ComponentFixture<Links>;
  let component: Links;
  let http: HttpTestingController;
  const route = '/api/v1/profile/links';
  const item: Link = {
    id: 'link-1', name: 'Portfolio', url: 'https://example.com',
    createdAtUtc: '2025-01-01T00:00:00Z', updatedAtUtc: '2025-01-01T00:00:00Z'
  };
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Links], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Links);
    component = fixture.componentInstance;
  });
  afterEach(() => { http.verify(); fixture.destroy(); });
  function load(items: Link[] = []) { http.expectOne(route).flush(items); fixture.detectChanges(); }

  it('retries a failed load and renders the empty state', () => {
    http.expectOne(route).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Impossible de charger');
    component['loadLinks'](); load();
    expect(fixture.nativeElement.textContent).toContain('Aucun lien');
  });

  it('trims the name and URL and prevents duplicate creation', () => {
    load(); component['startAdding']();
    component['form'].setValue({ name: ' Portfolio ', url: ' https://example.com ' });
    component['saveLink'](); component['saveLink']();
    const request = http.expectOne(route);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Portfolio', url: 'https://example.com' });
    request.flush(item); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('https://example.com');
    const anchor = fixture.nativeElement.querySelector('a');
    expect(anchor.getAttribute('target')).toBe('_blank');
    expect(anchor.getAttribute('rel')).toBe('noopener noreferrer');
    expect(component['adding']()).toBe(false);
  });

  it.each([
    { name: ' ', url: 'https://github.com/example' },
    { name: 'GitHub', url: ' ' },
    { name: 'a'.repeat(101), url: 'https://github.com/example' },
    { name: 'GitHub', url: 'a'.repeat(2001) }
  ])('rejects invalid fields %o', command => {
    load(); component['startAdding'](); component['form'].setValue(command);
    component['saveLink'](); http.expectNone(route);
    expect(component['form'].invalid).toBe(true);
  });

  it('prefills fields and retains edits after a failed update', () => {
    load([item]); component['startEditing'](item);
    expect(component['form'].getRawValue()).toEqual({ name: item.name, url: item.url });
    component['form'].controls.url.setValue('https://example.com/updated');
    component['saveLink']();
    const request = http.expectOne(route + '/link-1');
    expect(request.request.method).toBe('PUT');
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(component['form'].controls.url.value).toBe('https://example.com/updated');
    expect(component['saveError']()).toBeTruthy();
    component['saveLink']();
    http.expectOne(route + '/link-1').flush({ ...item, url: 'https://example.com/updated' });
    expect(component['links']().length).toBe(1);
    expect(component['links']()[0].url).toBe('https://example.com/updated');
  });

  it('requires confirmation and retries deletion without losing the item on failure', () => {
    load([item]); component['deleteLink'](item.id); http.expectNone(route + '/link-1');
    component['requestDelete'](item.id); component['cancelDelete']();
    expect(component['links']().length).toBe(1);
    component['requestDelete'](item.id); component['deleteLink'](item.id); component['deleteLink'](item.id);
    const request = http.expectOne(route + '/link-1');
    expect(request.request.method).toBe('DELETE');
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(component['links']().length).toBe(1);
    component['deleteLink'](item.id); http.expectOne(route + '/link-1').flush(null);
    expect(component['links']()).toEqual([]);
  });

  it('sorts links alphabetically and cancels editing without saving', () => {
    load([item, { ...item, id: '2', name: 'GitHub', url: 'https://github.com/example' }]);
    expect(component['links']()[0].name).toBe('GitHub');
    component['startEditing'](item); component['cancelForm']();
    expect(component['editingId']()).toBeNull();
    http.expectNone(route + '/link-1');
  });

  it.each(['javascript:alert(1)', 'ftp://example.com', '/relative', 'example.com'])('rejects unsafe or nonabsolute URL %s', url => {
    load(); component['startAdding']();
    component['form'].setValue({ name: 'Portfolio', url });
    component['saveLink'](); http.expectNone(route);
    expect(component['form'].controls.url.invalid).toBe(true);
  });

  it('does not render unsafe stored URLs as links', () => {
    load([{ ...item, url: 'javascript:alert(1)' }]);
    expect(fixture.nativeElement.querySelector('a')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('URL invalide');
  });
});

import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Languages } from './languages';
import { Language } from '../../models/language.model';

describe('Languages', () => {
  let fixture: ComponentFixture<Languages>;
  let component: Languages;
  let http: HttpTestingController;
  const route = '/api/v1/profile/languages';
  const item: Language = {
    id: 'language-1', name: 'Français', proficiencyLevel: 'Langue maternelle',
    createdAtUtc: '2025-01-01T00:00:00Z', updatedAtUtc: '2025-01-01T00:00:00Z'
  };
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Languages], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Languages);
    component = fixture.componentInstance;
  });
  afterEach(() => { http.verify(); fixture.destroy(); });
  function load(items: Language[] = []) { http.expectOne(route).flush(items); fixture.detectChanges(); }

  it('retries a failed load and renders the empty state', () => {
    http.expectOne(route).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Impossible de charger');
    component['loadLanguages'](); load();
    expect(fixture.nativeElement.textContent).toContain('Aucune langue');
  });

  it('trims both required fields and prevents duplicate creation', () => {
    load(); component['startAdding']();
    component['form'].setValue({ name: ' Français ', proficiencyLevel: ' Langue maternelle ' });
    component['saveLanguage'](); component['saveLanguage']();
    const request = http.expectOne(route);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Français', proficiencyLevel: 'Langue maternelle' });
    request.flush(item); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Langue maternelle');
    expect(component['adding']()).toBe(false);
  });

  it.each([
    { name: ' ', proficiencyLevel: 'B2' },
    { name: 'Anglais', proficiencyLevel: ' ' },
    { name: 'a'.repeat(101), proficiencyLevel: 'B2' },
    { name: 'Anglais', proficiencyLevel: 'a'.repeat(51) }
  ])('rejects invalid fields %o', command => {
    load(); component['startAdding'](); component['form'].setValue(command);
    component['saveLanguage'](); http.expectNone(route);
    expect(component['form'].invalid).toBe(true);
  });

  it('prefills fields and retains edits after a failed update', () => {
    load([item]); component['startEditing'](item);
    expect(component['form'].getRawValue()).toEqual({ name: item.name, proficiencyLevel: item.proficiencyLevel });
    component['form'].controls.proficiencyLevel.setValue('C2');
    component['saveLanguage']();
    const request = http.expectOne(route + '/language-1');
    expect(request.request.method).toBe('PUT');
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(component['form'].controls.proficiencyLevel.value).toBe('C2');
    expect(component['saveError']()).toBeTruthy();
    component['saveLanguage']();
    http.expectOne(route + '/language-1').flush({ ...item, proficiencyLevel: 'C2' });
    expect(component['languages']().length).toBe(1);
    expect(component['languages']()[0].proficiencyLevel).toBe('C2');
  });

  it('requires confirmation and retries deletion without losing the item on failure', () => {
    load([item]); component['deleteLanguage'](item.id); http.expectNone(route + '/language-1');
    component['requestDelete'](item.id); component['cancelDelete']();
    expect(component['languages']().length).toBe(1);
    component['requestDelete'](item.id); component['deleteLanguage'](item.id); component['deleteLanguage'](item.id);
    const request = http.expectOne(route + '/language-1');
    expect(request.request.method).toBe('DELETE');
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(component['languages']().length).toBe(1);
    component['deleteLanguage'](item.id); http.expectOne(route + '/language-1').flush(null);
    expect(component['languages']()).toEqual([]);
  });

  it('sorts languages alphabetically and cancels editing without saving', () => {
    load([item, { ...item, id: '2', name: 'Anglais', proficiencyLevel: 'B2' }]);
    expect(component['languages']()[0].name).toBe('Anglais');
    component['startEditing'](item); component['cancelForm']();
    expect(component['editingId']()).toBeNull();
    http.expectNone(route + '/language-1');
  });
});

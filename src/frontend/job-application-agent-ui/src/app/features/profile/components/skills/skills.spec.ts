import { TestBed, ComponentFixture } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Skills } from './skills';
import { Skill } from '../../models/skill.model';

describe('Skills', () => {
  let fixture: ComponentFixture<Skills>;
  let component: Skills;
  let http: HttpTestingController;
  const route = '/api/v1/profile/skills';
  const item: Skill = {
    id: 'skill-1', name: 'Angular', category: 'Frontend', level: 'Avancé', yearsOfExperience: 0,
    createdAtUtc: '2025-01-01T00:00:00Z', updatedAtUtc: '2025-01-01T00:00:00Z'
  };
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Skills], providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Skills);
    component = fixture.componentInstance;
  });
  afterEach(() => { http.verify(); fixture.destroy(); });
  function load(items: Skill[] = []) { http.expectOne(route).flush(items); fixture.detectChanges(); }

  it('retries a failed load and displays the empty state', () => {
    http.expectOne(route).flush({}, { status: 500, statusText: 'Error' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Impossible de charger');
    component['loadSkills'](); load();
    expect(fixture.nativeElement.textContent).toContain('Aucune compétence');
  });

  it('creates trimmed values, nullable optional fields and blocks duplicate requests', () => {
    load(); component['startAdding']();
    component['form'].controls.name.setValue(' Angular ');
    component['saveSkill'](); component['saveSkill']();
    const request = http.expectOne(route);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ name: 'Angular', category: null, level: null, yearsOfExperience: null });
    request.flush(item);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('0 année');
    expect(component['adding']()).toBe(false);
  });

  it.each([-1, 1.5, 2147483648])('rejects invalid years %s', years => {
    load(); component['startAdding']();
    component['form'].patchValue({ name: 'Angular', yearsOfExperience: years });
    component['saveSkill'](); http.expectNone(route);
    expect(component['form'].controls.yearsOfExperience.invalid).toBe(true);
  });

  it('rejects whitespace and overlong fields', () => {
    load(); component['startAdding']();
    component['form'].controls.name.setValue('  ');
    component['saveSkill'](); http.expectNone(route);
    component['form'].patchValue({ name: 'Angular', category: 'a'.repeat(101), level: 'a'.repeat(51) });
    component['saveSkill'](); http.expectNone(route);
    expect(component['form'].controls.category.invalid).toBe(true);
    expect(component['form'].controls.level.invalid).toBe(true);
  });

  it('prefills zero, preserves input on failure and clears optional values on update', () => {
    load([item]); component['startEditing'](item);
    expect(component['form'].controls.yearsOfExperience.value).toBe(0);
    component['form'].patchValue({ name: 'TypeScript', category: '', level: '', yearsOfExperience: null });
    component['saveSkill']();
    const request = http.expectOne(route + '/skill-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ name: 'TypeScript', category: null, level: null, yearsOfExperience: null });
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(component['form'].controls.name.value).toBe('TypeScript');
    expect(component['saveError']()).toBeTruthy();
    component['saveSkill']();
    http.expectOne(route + '/skill-1').flush({ ...item, name: 'TypeScript', category: null, level: null, yearsOfExperience: null });
    expect(component['skills']().length).toBe(1);
    expect(component['skills']()[0].name).toBe('TypeScript');
  });

  it('requires confirmation and retries failed deletion', () => {
    load([item]); component['deleteSkill'](item.id); http.expectNone(route + '/skill-1');
    component['requestDelete'](item.id); component['cancelDelete']();
    expect(component['skills']().length).toBe(1);
    component['requestDelete'](item.id); component['deleteSkill'](item.id); component['deleteSkill'](item.id);
    const request = http.expectOne(route + '/skill-1');
    expect(request.request.method).toBe('DELETE');
    request.flush({}, { status: 500, statusText: 'Error' });
    expect(component['skills']().length).toBe(1);
    component['deleteSkill'](item.id);
    http.expectOne(route + '/skill-1').flush(null);
    expect(component['skills']()).toEqual([]);
  });

  it('sorts alphabetically and cancels editing without a request', () => {
    load([{ ...item, id: '2', name: 'TypeScript' }, item]);
    expect(component['skills']()[0].name).toBe('Angular');
    component['startEditing'](item); component['cancelForm']();
    expect(component['editingId']()).toBeNull();
    http.expectNone(route + '/skill-1');
  });
});

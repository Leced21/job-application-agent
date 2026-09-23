import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { PersonalInformation } from './personal-information';

 describe('PersonalInformation', () => {
  const profile = { id: '1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.com', phoneNumber: null, jobTitle: null, summary: null, createdAtUtc: '', updatedAtUtc: '' };
  function setup() {
    TestBed.configureTestingModule({ imports: [PersonalInformation], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(PersonalInformation);
    fixture.componentRef.setInput('profile', profile);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
  }
  it('saves once and emits the updated profile', () => {
    const { fixture, component, http } = setup();
    const emitted = vi.fn(); component.profileUpdated.subscribe(emitted);
    component['startEditing'](); fixture.detectChanges();
    component['form'].patchValue({ firstName: ' Jeanne ', phoneNumber: '  ' });
    component['saveProfile'](); component['saveProfile']();
    const request = http.expectOne('/api/v1/profile');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body.firstName).toBe('Jeanne');
    expect(request.request.body.phoneNumber).toBeNull();
    const updated = { ...profile, firstName: 'Jeanne' };
    request.flush(updated);
    expect(emitted).toHaveBeenCalledWith(updated);
    expect(component['editing']()).toBe(false);
    http.verify(); fixture.destroy();
  });
  it('rejects whitespace names and overlong fields without a request', () => {
    const { fixture, component, http } = setup();
    component['startEditing']();
    component['form'].patchValue({ firstName: '   ', summary: 'x'.repeat(2001) });
    component['saveProfile'](); fixture.detectChanges();
    expect(component['form'].invalid).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('2000 caractères maximum');
    http.expectNone('/api/v1/profile'); http.verify(); fixture.destroy();
  });
  it('retains edits after a failed save and allows cancellation', () => {
    const { fixture, component, http } = setup();
    component['startEditing'](); component['form'].controls.jobTitle.setValue('Developer');
    component['saveProfile'](); component['cancelEditing']();
    expect(component['editing']()).toBe(true);
    http.expectOne('/api/v1/profile').flush({}, { status: 500, statusText: 'Error' });
    expect(component['form'].controls.jobTitle.value).toBe('Developer');
    expect(component['saveError']()).toBeTruthy();
    component['cancelEditing']();
    expect(component['editing']()).toBe(false);
    expect(component.profile()).toEqual(profile);
    http.verify(); fixture.destroy();
  });
});

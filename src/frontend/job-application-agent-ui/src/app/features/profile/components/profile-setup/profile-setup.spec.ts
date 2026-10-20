import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProfileSetup } from './profile-setup';

describe('ProfileSetup', () => {
  function setup() {
    TestBed.configureTestingModule({
      imports: [ProfileSetup],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const fixture = TestBed.createComponent(ProfileSetup);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/auth/session').flush({
      isAuthenticated: true,
      email: 'candidate@example.test',
      registrationEnabled: true,
    });
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http };
  }

  it('prefills the account email and creates a normalized candidate profile', () => {
    const { fixture, component, http } = setup();
    const created = vi.fn();
    component.profileCreated.subscribe(created);
    component['form'].patchValue({
      firstName: ' Camille ',
      lastName: ' Martin ',
      phoneNumber: '  ',
      jobTitle: ' Data Engineer ',
      summary: ' En recherche ',
    });

    component['createProfile']();
    const request = http.expectOne('/api/v1/profile');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      firstName: 'Camille',
      lastName: 'Martin',
      email: 'candidate@example.test',
      phoneNumber: null,
      jobTitle: 'Data Engineer',
      summary: 'En recherche',
    });

    const profile = {
      id: 'profile-1',
      firstName: 'Camille',
      lastName: 'Martin',
      email: 'candidate@example.test',
      phoneNumber: null,
      jobTitle: 'Data Engineer',
      summary: 'En recherche',
      createdAtUtc: '',
      updatedAtUtc: '',
    };
    request.flush(profile, { status: 201, statusText: 'Created' });
    expect(created).toHaveBeenCalledWith(profile);
    expect(component['saving']()).toBe(false);
    http.verify();
    fixture.destroy();
  });

  it('rejects blank names and invalid email without sending a request', () => {
    const { fixture, component, http } = setup();
    component['form'].patchValue({ firstName: '  ', lastName: '', email: 'invalid' });

    component['createProfile']();
    fixture.detectChanges();

    expect(component['form'].invalid).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Le prénom est obligatoire');
    http.expectNone('/api/v1/profile');
    http.verify();
    fixture.destroy();
  });

  it('shows a safe message when profile creation fails', () => {
    const { fixture, component, http } = setup();
    component['form'].patchValue({ firstName: 'Camille', lastName: 'Martin' });
    component['createProfile']();
    http.expectOne('/api/v1/profile').flush({}, { status: 500, statusText: 'Error' });

    expect(component['creationError']()).toContain('n’a pas pu être créé');
    expect(component['saving']()).toBe(false);
    http.verify();
    fixture.destroy();
  });
});

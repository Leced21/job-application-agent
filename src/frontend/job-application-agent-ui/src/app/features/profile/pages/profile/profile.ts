import { PersonalInformation } from '../../components/personal-information/personal-information';
import { Preferences } from '../../components/preferences/preferences';
import { Links } from '../../components/links/links';
import { Languages } from '../../components/languages/languages';
import { Skills } from '../../components/skills/skills';
import { Educations } from '../../components/educations/educations';
import { Certifications } from '../../components/certifications/certifications';
import { Component, inject, signal } from '@angular/core';
import { ProfileApiService } from '../../data-access/profile-api.service';
import { CandidateProfile } from '../../models/candidate-profile.model';
import { Experiences } from '../../components/experiences/experiences';

@Component({
  selector: 'app-profile',
  imports: [PersonalInformation, Experiences, Certifications, Educations, Skills, Languages, Links, Preferences],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly profileApi = inject(ProfileApiService);

  protected readonly profile = signal<CandidateProfile | null>(null);
  protected readonly loading = signal(true);

  protected readonly error = signal<string | null>(null);

  constructor() {
    this.loadProfile();
  }

  private loadProfile(): void {
    this.profileApi.getProfile().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Failed to load profile', error);
        this.error.set('Failed to load profile');
        this.loading.set(false);
      }
    });
  }

}

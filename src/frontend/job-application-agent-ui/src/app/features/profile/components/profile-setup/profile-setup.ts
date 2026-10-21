import { HttpErrorResponse } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AuthApiService } from '../../../../core/auth/auth-api.service';
import { ProfileApiService } from '../../data-access/profile-api.service';
import { CandidateProfile } from '../../models/candidate-profile.model';
import { CreateCandidateProfile } from '../../models/create-candidate-profile.model';

@Component({
  selector: 'app-profile-setup',
  imports: [ReactiveFormsModule],
  templateUrl: './profile-setup.html',
  styleUrl: './profile-setup.scss',
})
export class ProfileSetup {
  private readonly destroyRef = inject(DestroyRef);
  private readonly formBuilder = inject(FormBuilder);
  private readonly authApi = inject(AuthApiService);
  private readonly profileApi = inject(ProfileApiService);

  readonly profileCreated = output<CandidateProfile>();
  protected readonly saving = signal(false);
  protected readonly creationError = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    firstName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(255)]],
    phoneNumber: ['', Validators.maxLength(30)],
    jobTitle: ['', Validators.maxLength(150)],
    summary: ['', Validators.maxLength(2000)],
  });

  constructor() {
    this.authApi.session().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (session) => {
        if (session.email) this.form.controls.email.setValue(session.email);
      },
    });
  }

  protected createProfile(): void {
    if (this.saving()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const command: CreateCandidateProfile = {
      firstName: value.firstName.trim(),
      lastName: value.lastName.trim(),
      email: value.email.trim(),
      phoneNumber: this.toNullable(value.phoneNumber),
      jobTitle: this.toNullable(value.jobTitle),
      summary: this.toNullable(value.summary),
    };

    this.saving.set(true);
    this.creationError.set(null);
    this.profileApi.createProfile(command).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (profile) => {
        this.saving.set(false);
        this.profileCreated.emit(profile);
      },
      error: (error: HttpErrorResponse) => {
        this.saving.set(false);
        this.creationError.set(error.status === 409
          ? 'Un profil existe déjà pour ce compte. Rechargez la page pour l’afficher.'
          : 'Votre profil n’a pas pu être créé. Vérifiez les informations puis réessayez.');
      },
    });
  }

  private toNullable(value: string): string | null {
    const trimmed = value.trim();
    return trimmed.length > 0 ? trimmed : null;
  }
}

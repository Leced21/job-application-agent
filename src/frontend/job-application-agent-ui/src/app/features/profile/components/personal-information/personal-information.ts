import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
    Component,
    DestroyRef,
    inject,
    input,
    output,
    signal
} from '@angular/core';
import {
    FormBuilder,
    ReactiveFormsModule,
    Validators
} from '@angular/forms';

import { ProfileApiService } from '../../data-access/profile-api.service';
import { CandidateProfile } from '../../models/candidate-profile.model';
import { UpdateCandidateProfile } from '../../models/update-candidate-profile.model';

@Component({
    selector: 'app-personal-information',
    imports: [ReactiveFormsModule],
    templateUrl: './personal-information.html',
    styleUrl: './personal-information.scss'
})
export class PersonalInformation {
    private readonly destroyRef = inject(DestroyRef);
    private readonly profileApi = inject(ProfileApiService);
    private readonly formBuilder = inject(FormBuilder);

    readonly profile = input.required<CandidateProfile>();
    readonly profileUpdated = output<CandidateProfile>();

    protected readonly editing = signal(false);
    protected readonly saving = signal(false);
    protected readonly saveError = signal<string | null>(null);

    protected readonly form = this.formBuilder.nonNullable.group({
        firstName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
        lastName: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
        email: ['', [
            Validators.required,
            Validators.email,
            Validators.maxLength(255)
        ]],
        phoneNumber: ['', Validators.maxLength(30)],
        jobTitle: ['', Validators.maxLength(150)],
        summary: ['', Validators.maxLength(2000)]
    });

    protected startEditing(): void {
        if (this.saving()) return;
        const candidate = this.profile();

        this.form.reset({
            firstName: candidate.firstName,
            lastName: candidate.lastName,
            email: candidate.email,
            phoneNumber: candidate.phoneNumber ?? '',
            jobTitle: candidate.jobTitle ?? '',
            summary: candidate.summary ?? ''
        });

        this.saveError.set(null);
        this.editing.set(true);
    }

    protected cancelEditing(): void {
        if (this.saving()) return;
        this.saveError.set(null);
        this.editing.set(false);
    }

    protected saveProfile(): void {
        if (this.saving()) return;
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const value = this.form.getRawValue();

        const command: UpdateCandidateProfile = {
            firstName: value.firstName.trim(),
            lastName: value.lastName.trim(),
            email: value.email.trim(),
            phoneNumber: this.toNullable(value.phoneNumber),
            jobTitle: this.toNullable(value.jobTitle),
            summary: this.toNullable(value.summary)
        };

        this.saving.set(true);
        this.saveError.set(null);

        this.profileApi.updateProfile(command).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
            next: (updatedProfile) => {
                this.editing.set(false);
                this.saving.set(false);
                this.profileUpdated.emit(updatedProfile);
            },

            error: (error) => {
                console.error(
                    'Unable to update candidate profile.',
                    error
                );

                this.saveError.set(
                    'Impossible d’enregistrer les modifications.'
                );

                this.saving.set(false);
            }
        });
    }

    private toNullable(value: string): string | null {
        const trimmedValue = value.trim();

        return trimmedValue.length === 0
            ? null
            : trimmedValue;
    }
}
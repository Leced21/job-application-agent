import { Component, inject, signal } from '@angular/core';

import { ProfileApiService } from '../../data-access/profile-api.service';
import { ProfessionalExperience } from '../../models/professional-experience.model';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';

@Component({
    selector: 'app-experiences',
    imports: [ReactiveFormsModule],
    templateUrl: './experiences.html',
    styleUrl: './experiences.scss'
})
export class Experiences {
    private readonly profileApi = inject(ProfileApiService);
    private readonly formBuilder = inject(FormBuilder);

    protected readonly experiences = signal<ProfessionalExperience[]>([]);
    protected readonly loading = signal(true);
    protected readonly error = signal<string | null>(null);

    protected readonly adding = signal(false);
    protected readonly saving = signal(false);
    protected readonly saveError = signal<string | null>(null);
    protected readonly editingId = signal<string | null>(null);

    protected readonly deletingId = signal<string | null>(null);
    protected readonly deleteError = signal<string | null>(null);

    protected readonly form = this.formBuilder.nonNullable.group({
        companyName: ['', [
            Validators.required,
            Validators.maxLength(200)
        ]],
        jobTitle: ['', [
            Validators.required,
            Validators.maxLength(150)
        ]],
        location: ['', Validators.maxLength(200)],
        startDate: ['', Validators.required],
        endDate: [''],
        isCurrent: [false],
        description: ['', Validators.maxLength(4000)]
    });
    protected startEditing(experience: ProfessionalExperience): void {
        this.form.reset({
            companyName: experience.companyName,
            jobTitle: experience.jobTitle,
            location: experience.location ?? '',
            startDate: experience.startDate,
            endDate: experience.endDate ?? '',
            isCurrent: experience.isCurrent,
            description: experience.description ?? ''
        });

        if (experience.isCurrent) {
            this.form.controls.endDate.disable();
        } else {
            this.form.controls.endDate.enable();
        }

        this.saveError.set(null);
        this.adding.set(false);
        this.editingId.set(experience.id);
    }

    constructor() {
        this.loadExperiences();

        this.form.controls.isCurrent.valueChanges.subscribe((isCurrent) => {
            if (isCurrent) {
                this.form.controls.endDate.setValue('');
                this.form.controls.endDate.disable();
            } else {
                this.form.controls.endDate.enable();
            }
        });
    }

    protected startAdding(): void {
        this.editingId.set(null);

        this.form.reset({
            companyName: '',
            jobTitle: '',
            location: '',
            startDate: '',
            endDate: '',
            isCurrent: false,
            description: ''
        });
        this.form.controls.endDate.enable();
        this.saveError.set(null);
        this.adding.set(true);
    }

    protected cancelForm(): void {
        this.adding.set(false);
        this.editingId.set(null);
        this.saveError.set(null);
    }

    protected saveExperience(): void {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }
        const value = this.form.getRawValue();
        if (
            !value.isCurrent &&
            value.endDate &&
            value.endDate < value.startDate
        ) {
            this.saveError.set('La date de fin doit être postérieure ou égale à la date de début.');
            return;
        }
        const command = {
            companyName: value.companyName.trim(),
            jobTitle: value.jobTitle.trim(),
            location: this.toNullable(value.location),
            startDate: value.startDate,
            endDate: value.isCurrent ? null : this.toNullable(value.endDate),
            isCurrent: value.isCurrent,
            description: this.toNullable(value.description)
        };
        this.saving.set(true);
        this.saveError.set(null);

        const experienceId = this.editingId();

        const request = experienceId
            ? this.profileApi.updateExperience(experienceId, command)
            : this.profileApi.addExperience(command);
        request.subscribe({
            next: (savedExperience) => {
                this.experiences.update((experiences) => {
                    const updatedExperiences = experienceId
                        ? experiences.map((experience) =>
                            experience.id === savedExperience.id ? savedExperience : experience)
                        : [savedExperience, ...experiences];
                    return updatedExperiences.sort((a, b) => b.startDate.localeCompare(a.startDate));
                });
                this.adding.set(false);
                this.editingId.set(null);
                this.saving.set(false);
            },
            error: (error) => {
                console.error('Unable to save professional experience.', error);
                this.saveError.set('Impossible d\'enregistrer l\'expérience professionnelle.');
                this.saving.set(false);
            }
        });
    }
    protected formatDate(date: string | null): string {
        if (!date) {
            return '';
        }

        const [year, month] = date.split('-');

        return new Intl.DateTimeFormat('fr-FR', {
            month: 'short',
            year: 'numeric'
        }).format(
            new Date(Number(year), Number(month) - 1, 1)
        );
    }
    private loadExperiences(): void {
        this.profileApi.getExperiences().subscribe({
            next: (experiences) => {
                this.experiences.set(experiences);
                this.loading.set(false);
            },
            error: (error) => {
                console.error('Unable to load professional experiences.', error);
                this.error.set(
                    'Impossible de charger les expériences professionnelles.'
                );
                this.loading.set(false);
            }
        });
    }
    protected requestDelete(experienceId: string): void {
        this.deleteError.set(null);
        this.deletingId.set(experienceId);
    }

    protected cancelDelete(): void {
        this.deletingId.set(null);
        this.deleteError.set(null);
    }

    protected deleteExperience(experienceId: string): void {
        this.deleteError.set(null);

        this.profileApi.deleteExperience(experienceId).subscribe({
            next: () => {
                this.experiences.update((experiences) =>
                    experiences.filter(
                        (experience) => experience.id !== experienceId
                    )
                );

                if (this.editingId() === experienceId) {
                    this.editingId.set(null);
                }

                this.deletingId.set(null);
            },

            error: (error) => {
                console.error(
                    'Unable to delete professional experience.',
                    error
                );

                this.deleteError.set(
                    'Impossible de supprimer cette expérience.'
                );
            }
        });
    }
    private toNullable(value: string): string | null {
        const trimmedValue = value.trim();
        return trimmedValue.length === 0 ? null : trimmedValue;
    }
}

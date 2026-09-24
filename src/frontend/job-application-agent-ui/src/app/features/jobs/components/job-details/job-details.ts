import { Component, input } from '@angular/core';

import { ContractType } from '../../models/contract-type.model';
import { WorkMode } from '../../models/work-mode.model';
import { Job } from '../../models/job.model';

@Component({
    selector: 'app-job-details',
    imports: [],
    templateUrl: './job-details.html',
    styleUrl: './job-details.scss'
})
export class JobDetails {
    readonly job = input.required<Job>();


    protected get companyInitial(): string {
        return this.job().companyName
            .trim()
            .charAt(0)
            .toUpperCase();
    }

    protected get locationLabel(): string {
        return this.job().location ?? 'Localisation non renseignée';
    }

    protected get workModeLabel(): string {
        switch (this.job().workMode) {
            case WorkMode.OnSite:
                return 'Sur site';
            case WorkMode.Hybrid:
                return 'Hybride';
            case WorkMode.Remote:
                return 'Télétravail';
            default:
                return 'Non précisé';
        }
    }

    protected get contractTypeLabel(): string {
        switch (this.job().contractType) {
            case ContractType.Permanent:
                return 'CDI';
            case ContractType.FixedTerm:
                return 'CDD';
            case ContractType.Freelance:
                return 'Freelance';
            case ContractType.Internship:
                return 'Stage';
            case ContractType.Apprenticeship:
                return 'Alternance';
            case ContractType.Temporary:
                return 'Intérim';
            default:
                return 'Non précisé';
        }
    }

    protected get salaryLabel(): string | null {
        const job = this.job();

        if (job.salaryMin && job.salaryMax) {
            return `${job.salaryMin.toLocaleString('fr-FR')} – ${job.salaryMax.toLocaleString('fr-FR')} ${job.salaryCurrency ?? '€'}`;
        }

        if (job.salaryMin) {
            return `À partir de ${job.salaryMin.toLocaleString('fr-FR')} ${job.salaryCurrency ?? '€'}`;
        }

        if (job.salaryMax) {
            return `Jusqu’à ${job.salaryMax.toLocaleString('fr-FR')} ${job.salaryCurrency ?? '€'}`;
        }

        return null;
    }
    protected get publishedLabel(): string {
        const value =
            this.job().publishedAtUtc ??
            this.job().createdAtUtc;

        return new Intl.DateTimeFormat('fr-FR', {
            day: 'numeric',
            month: 'long',
            year: 'numeric'
        }).format(new Date(value));
    }
}
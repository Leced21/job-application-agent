import { Component, input, output } from '@angular/core';

import { Job } from '../../models/job.model';
import { ContractType } from '../../models/contract-type.model';
import { WorkMode } from '../../models/work-mode.model';

@Component({
    selector: 'app-job-card',
    imports: [],
    templateUrl: './job-card.html',
    styleUrl: './job-card.scss'
})
export class JobCard {
    readonly job = input.required<Job>();
    readonly selected = input(false);

    readonly jobSelected = output<Job>();

    protected selectJob(): void {
        this.jobSelected.emit(this.job());
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
    protected get companyInitial(): string {
        return this.job().companyName
            .trim()
            .charAt(0)
            .toUpperCase();
    }

    protected get locationLabel(): string {
        return this.job().location ?? 'Localisation non renseignée';
    }

    protected get publishedLabel(): string {
        const value = this.job().publishedAtUtc ?? this.job().createdAtUtc;

        const published = new Date(value);
        const now = new Date();

        const diffMs = now.getTime() - published.getTime();
        const diffDays = Math.max(
            0,
            Math.floor(diffMs / (1000 * 60 * 60 * 24))
        );

        if (diffDays === 0) {
            return "Aujourd’hui";
        }

        if (diffDays === 1) {
            return 'Hier';
        }

        if (diffDays < 7) {
            return `Il y a ${diffDays} jours`;
        }

        return published.toLocaleDateString('fr-FR', {
            day: 'numeric',
            month: 'short'
        });
    }
    protected get salaryLabel(): string | null {
        const job = this.job();

        if (job.salaryMin === null && job.salaryMax === null) {
            return null;
        }

        const currency = job.salaryCurrency ?? 'EUR';

        const formatter = new Intl.NumberFormat('fr-FR', {
            style: 'currency',
            currency,
            maximumFractionDigits: 0
        });

        if (job.salaryMin !== null && job.salaryMax !== null) {
            return `${formatter.format(job.salaryMin)} – ${formatter.format(job.salaryMax)}`;
        }

        if (job.salaryMin !== null) {
            return `À partir de ${formatter.format(job.salaryMin)}`;
        }

        return `Jusqu’à ${formatter.format(job.salaryMax!)}`;
    }
}
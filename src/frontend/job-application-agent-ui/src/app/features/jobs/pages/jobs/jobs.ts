import { Component, inject, signal } from '@angular/core';

import { JobsApiService } from '../../data-access/jobs-api.service';
import { Job } from '../../models/job.model';
import { JobCard } from '../../components/job-card/job-card';
import { JobDetails } from '../../components/job-details/job-details';
import { JobFilters, JobFiltersValue } from '../../components/job-filters/job-filters';
import { WorkMode } from '../../models/work-mode.model';


type JobsTab = 'all' | 'new';
type JobsSort = 'recent' | 'salary-desc' | 'salary-asc';
@Component({
    selector: 'app-jobs',
    imports: [JobCard, JobDetails, JobFilters],
    templateUrl: './jobs.html',
    styleUrl: './jobs.scss'
})

export class Jobs {
    private readonly jobsApi = inject(JobsApiService);

    protected readonly jobs = signal<Job[]>([]);
    protected readonly loading = signal(true);
    protected readonly error = signal<string | null>(null);
    protected readonly selectedJob = signal<Job | null>(null);
    protected readonly filteredJobs = signal<Job[]>([]);
    protected readonly activeTab = signal<JobsTab>('all');
    protected readonly sort = signal<JobsSort>('recent');
    protected readonly globalSearch = signal('');
    protected readonly filters = signal<JobFiltersValue>({
        keywords: '',
        location: '',
        remoteOnly: false,
        contractTypes: [],
        salaryMin: null
    });

    protected selectJob(job: Job): void {
        this.selectedJob.set(job);
    }

    constructor() {
        this.loadJobs();
    }

    private loadJobs(): void {
        this.jobsApi.getAll().subscribe({
            next: jobs => {
                this.jobs.set(jobs);
                this.applyFilters(this.filters());
                this.loading.set(false);
            },
            error: error => {
                console.error('Failed to load job offers', error);
                this.error.set(
                    'Impossible de charger les offres d’emploi.'
                );
                this.loading.set(false);
            }
        });
    }
    protected applyFilters(filters: JobFiltersValue): void {
        this.filters.set(filters);

        const keywords =
            filters.keywords.trim().toLowerCase();

        const location =
            filters.location.trim().toLowerCase();

        const globalSearch =
            this.globalSearch()
                .trim()
                .toLowerCase();

        const filtered = this.jobs().filter(job => {
            const matchesKeywords =
                !keywords ||
                job.title.toLowerCase().includes(keywords) ||
                job.companyName.toLowerCase().includes(keywords) ||
                job.description?.toLowerCase().includes(keywords);

            const matchesLocation =
                !location ||
                job.location?.toLowerCase().includes(location);

            const matchesRemote =
                !filters.remoteOnly ||
                job.workMode === WorkMode.Remote;

            const matchesContractType =
                filters.contractTypes.length === 0 ||
                filters.contractTypes.includes(job.contractType);

            const matchesSalary =
                filters.salaryMin === null ||
                (job.salaryMax !== null &&
                    job.salaryMax >= filters.salaryMin);

            const matchesTab =
                this.activeTab() === 'all' ||
                (this.activeTab() === 'new' && this.isNew(job));

            const matchesGlobalSearch =
                !globalSearch ||
                job.title.toLowerCase().includes(globalSearch) ||
                job.companyName.toLowerCase().includes(globalSearch) ||
                job.location?.toLowerCase().includes(globalSearch) ||
                job.description?.toLowerCase().includes(globalSearch) ||
                job.source.toLowerCase().includes(globalSearch);

            return (
                matchesKeywords &&
                matchesLocation &&
                matchesRemote &&
                matchesContractType &&
                matchesSalary &&
                matchesTab &&
                matchesGlobalSearch
            );
        });

        const sorted = [...filtered].sort((a, b) => {
            switch (this.sort()) {
                case 'salary-desc':
                    return (b.salaryMax ?? b.salaryMin ?? 0) -
                        (a.salaryMax ?? a.salaryMin ?? 0);

                case 'salary-asc':
                    return (a.salaryMin ?? a.salaryMax ?? 0) -
                        (b.salaryMin ?? b.salaryMax ?? 0);

                case 'recent':
                default:
                    return this.getJobDate(b) - this.getJobDate(a);
            }
        });
        this.filteredJobs.set(sorted);
        const selectedStillExists =
            sorted.some(
                job => job.id === this.selectedJob()?.id
            );

        if (!selectedStillExists) {
            this.selectedJob.set(sorted[0] ?? null);
        }
    }
    protected selectTab(tab: JobsTab): void {
        this.activeTab.set(tab);
        this.applyFilters(this.filters());
    }
    protected changeSort(value: string): void {
        this.sort.set(value as JobsSort);
        this.applyFilters(this.filters());
    }

    protected isNew(job: Job): boolean {
        if (!job.publishedAtUtc) {
            return false;
        }

        const publishedAt = new Date(job.publishedAtUtc);
        const threshold = new Date();

        threshold.setDate(threshold.getDate() - 7);

        return publishedAt >= threshold;
    }
    protected get newJobsCount(): number {
        return this.jobs().filter(job => this.isNew(job)).length;
    }
    private getJobDate(job: Job): number {
        const value =
            job.publishedAtUtc ??
            job.createdAtUtc;

        return new Date(value).getTime();
    }
    protected search(value: string): void {
        this.globalSearch.set(value);
        this.applyFilters(this.filters());
    }
}

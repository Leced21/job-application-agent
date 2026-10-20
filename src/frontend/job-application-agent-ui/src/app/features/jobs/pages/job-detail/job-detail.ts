import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { EMPTY, catchError, switchMap } from 'rxjs';
import { JobDetails } from '../../components/job-details/job-details';
import { JobsApiService } from '../../data-access/jobs-api.service';
import { Job } from '../../models/job.model';

@Component({
  selector: 'app-job-detail-page',
  imports: [JobDetails, RouterLink],
  templateUrl: './job-detail.html',
  styleUrl: './job-detail.scss'
})
export class JobDetailPage {
  private readonly route = inject(ActivatedRoute);
  private readonly jobsApi = inject(JobsApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly job = signal<Job | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.route.paramMap.pipe(
      switchMap(params => {
        this.loading.set(true);
        this.error.set(null);
        this.job.set(null);
        return this.jobsApi.getById(params.get('id') ?? '').pipe(
          catchError((error: HttpErrorResponse) => {
            this.error.set(error.status === 404
              ? 'Cette offre est introuvable ou n’est plus disponible.'
              : 'Impossible de charger cette offre pour le moment.');
            this.loading.set(false);
            return EMPTY;
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: job => {
        this.job.set(job);
        this.loading.set(false);
      },
    });
  }
}

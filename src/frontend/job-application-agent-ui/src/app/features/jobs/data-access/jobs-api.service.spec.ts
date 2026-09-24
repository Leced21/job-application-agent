import { TestBed } from '@angular/core/testing';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { JobsApiService } from './jobs-api.service';
import { Job } from '../models/job.model';
import { WorkMode } from '../models/work-mode.model';
import { ContractType } from '../models/contract-type.model';
import { JobStatus } from '../models/job-status.model';

describe('JobsApiService', () => {
  let service: JobsApiService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        JobsApiService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(JobsApiService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should get all job offers', () => {
    const jobs: Job[] = [
      {
        id: 'job-1',
        title: 'Data Engineer',
        companyName: 'Test Company',
        location: 'Paris',
        workMode: WorkMode.Hybrid,
        contractType: ContractType.Permanent,
        salaryMin: 55000,
        salaryMax: 70000,
        salaryCurrency: 'EUR',
        description: 'Test job offer',
        source: 'Manual',
        sourceUrl: null,
        status: JobStatus.Discovered,
        publishedAtUtc: null,
        createdAtUtc: '2026-09-24T06:00:00Z',
        updatedAtUtc: '2026-09-24T06:00:00Z'
      }
    ];

    service.getAll().subscribe(result => {
      expect(result).toEqual(jobs);
    });

    const request =
      httpTesting.expectOne('/api/v1/jobs');

    expect(request.request.method).toBe('GET');

    request.flush(jobs);
  });

  it('should get a job offer by id', () => {
    const job: Job = {
      id: 'job-1',
      title: 'Data Engineer',
      companyName: 'Test Company',
      location: 'Paris',
      workMode: WorkMode.Hybrid,
      contractType: ContractType.Permanent,
      salaryMin: 55000,
      salaryMax: 70000,
      salaryCurrency: 'EUR',
      description: 'Test job offer',
      source: 'Manual',
      sourceUrl: null,
      status: JobStatus.Discovered,
      publishedAtUtc: null,
      createdAtUtc: '2026-09-24T06:00:00Z',
      updatedAtUtc: '2026-09-24T06:00:00Z'
    };

    service.getById(job.id).subscribe(result => {
      expect(result).toEqual(job);
    });

    const request =
      httpTesting.expectOne(
        `/api/v1/jobs/${job.id}`
      );

    expect(request.request.method).toBe('GET');

    request.flush(job);
  });
});
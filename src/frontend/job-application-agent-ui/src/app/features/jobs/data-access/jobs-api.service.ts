import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { Job } from '../models/job.model';

@Injectable({
  providedIn: 'root'
})
export class JobsApiService {
  private readonly http = inject(HttpClient);

  private readonly baseUrl = '/api/v1/jobs';

  getAll(): Observable<Job[]> {
    return this.http.get<Job[]>(this.baseUrl);
  }

  getById(id: string): Observable<Job> {
    return this.http.get<Job>(
      `${this.baseUrl}/${id}`
    );
  }
}
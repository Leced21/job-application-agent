import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ComposedDocument, CompositionWriteResult, DocumentExportMetadata, DocumentExportWriteResult, PreparedDraft, PreparedDraftSummary, ResumeMetadata, ResumeSeriesSummary, ResumeSeriesVersion, ResumeSeriesWriteResult } from '../models/document.models';

@Injectable({ providedIn: 'root' })
export class DocumentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/documents';

  listResumes(): Observable<ResumeMetadata[]> {
    return this.http.get<ResumeMetadata[]>(`${this.baseUrl}/resumes?page=1&pageSize=100`);
  }

  uploadResume(file: File): Observable<ResumeMetadata> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.http.post<ResumeMetadata>(`${this.baseUrl}/resumes`, body);
  }

  downloadResume(id: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/resumes/${encodeURIComponent(id)}/content`, { responseType: 'blob' });
  }

  listSeries(): Observable<ResumeSeriesSummary[]> {
    return this.http.get<ResumeSeriesSummary[]>(`${this.baseUrl}/resume-series?page=1&pageSize=100`);
  }

  getVersions(seriesId: string): Observable<ResumeSeriesVersion[]> {
    return this.http.get<ResumeSeriesVersion[]>(`${this.baseUrl}/resume-series/${encodeURIComponent(seriesId)}/versions?page=1&pageSize=100`);
  }

  createSeries(name: string, firstResumeId: string, label: string | null): Observable<ResumeSeriesWriteResult> {
    return this.http.post<ResumeSeriesWriteResult>(`${this.baseUrl}/resume-series`, { name, firstResumeId, label });
  }

  addVersion(seriesId: string, resumeId: string, label: string | null): Observable<ResumeSeriesWriteResult> {
    return this.http.post<ResumeSeriesWriteResult>(`${this.baseUrl}/resume-series/${encodeURIComponent(seriesId)}/versions`, { resumeId, label });
  }

  listDrafts(): Observable<PreparedDraftSummary[]> {
    return this.http.get<PreparedDraftSummary[]>(`${this.baseUrl}/drafts?page=1&pageSize=100`);
  }

  getDraft(id: string): Observable<PreparedDraft> {
    return this.http.get<PreparedDraft>(`${this.baseUrl}/drafts/${encodeURIComponent(id)}`);
  }

  getComposition(draftId: string): Observable<ComposedDocument> {
    return this.http.get<ComposedDocument>(`${this.baseUrl}/drafts/${encodeURIComponent(draftId)}/composition`);
  }

  composeDraft(draftId: string): Observable<CompositionWriteResult> {
    return this.http.post<CompositionWriteResult>(`${this.baseUrl}/drafts/${encodeURIComponent(draftId)}/composition`, null);
  }

  reviewComposition(draftId: string, contentSha256: string, decision: 'Approved' | 'Rejected', confirmed: true, note: string | null): Observable<ComposedDocument> {
    return this.http.post<ComposedDocument>(`${this.baseUrl}/drafts/${encodeURIComponent(draftId)}/composition/review`, { contentSha256, decision, confirmed, note });
  }

  listExports(): Observable<DocumentExportMetadata[]> {
    return this.http.get<DocumentExportMetadata[]>(`${this.baseUrl}/exports?page=1&pageSize=100`);
  }

  createExport(draftId: string, format: 'Pdf' | 'Docx', contentSha256: string): Observable<DocumentExportWriteResult> {
    return this.http.post<DocumentExportWriteResult>(`${this.baseUrl}/drafts/${encodeURIComponent(draftId)}/exports`, { format, contentSha256 });
  }

  downloadExport(id: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/exports/${encodeURIComponent(id)}/content`, { responseType: 'blob' });
  }
}

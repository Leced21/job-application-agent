import { DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { DocumentsApiService } from '../../data-access/documents-api.service';
import { ResumeMetadata, ResumeSeriesSummary, ResumeSeriesVersion } from '../../models/document.models';

@Component({
  selector: 'app-documents',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './documents.html',
  styleUrl: './documents.scss'
})
export class Documents {
  private readonly api = inject(DocumentsApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(false);
  protected readonly uploading = signal(false);
  protected readonly savingSeries = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly resumes = signal<ResumeMetadata[]>([]);
  protected readonly series = signal<ResumeSeriesSummary[]>([]);
  protected readonly expandedSeriesId = signal<string | null>(null);
  protected readonly versions = signal<ResumeSeriesVersion[]>([]);
  protected readonly versionsLoading = signal(false);
  protected readonly totalSize = computed(() => this.resumes().reduce((total, resume) => total + resume.length, 0));

  constructor() { this.load(); }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({ resumes: this.api.listResumes(), series: this.api.listSeries() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: result => {
          this.resumes.set(result.resumes);
          this.series.set(result.series);
          this.loading.set(false);
        },
        error: (error: HttpErrorResponse) => { this.handleError(error); this.loading.set(false); }
      });
  }

  protected upload(fileInput: HTMLInputElement): void {
    const file = fileInput.files?.[0];
    if (!file) return;
    const lowerName = file.name.toLowerCase();
    const mediaType = lowerName.endsWith('.pdf')
      ? 'application/pdf'
      : lowerName.endsWith('.docx')
        ? 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
        : null;
    if (!mediaType) {
      this.error.set('Choisissez un fichier au format PDF ou DOCX.');
      fileInput.value = '';
      return;
    }
    if (file.size === 0 || file.size > 5 * 1024 * 1024) {
      this.error.set('Le fichier doit peser entre 1 octet et 5 Mio.');
      fileInput.value = '';
      return;
    }
    const normalizedFile = file.type === mediaType ? file : new File([file], file.name, { type: mediaType });
    this.uploading.set(true);
    this.error.set(null);
    this.api.uploadResume(normalizedFile).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.uploading.set(false);
        fileInput.value = '';
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.handleError(error);
        this.uploading.set(false);
        fileInput.value = '';
      }
    });
  }

  protected download(resume: ResumeMetadata): void {
    this.api.downloadResume(resume.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = resume.fileName;
        link.click();
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
      },
      error: (error: HttpErrorResponse) => this.handleError(error)
    });
  }

  protected createSeries(name: string, resumeId: string, label: string): void {
    if (!name.trim() || !resumeId) {
      this.error.set('Indiquez un nom de dossier et sélectionnez le CV qui en sera la première version.');
      return;
    }
    this.writeSeries(() => this.api.createSeries(name, resumeId, label.trim() || null));
  }

  protected addVersion(seriesId: string, resumeId: string, label: string): void {
    if (!resumeId) {
      this.error.set('Sélectionnez un CV à ajouter à cet historique.');
      return;
    }
    this.writeSeries(() => this.api.addVersion(seriesId, resumeId, label.trim() || null));
  }

  protected toggleVersions(seriesId: string): void {
    if (this.expandedSeriesId() === seriesId) {
      this.expandedSeriesId.set(null);
      return;
    }
    this.expandedSeriesId.set(seriesId);
    this.versionsLoading.set(true);
    this.versions.set([]);
    this.api.getVersions(seriesId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: versions => { this.versions.set(versions); this.versionsLoading.set(false); },
      error: (error: HttpErrorResponse) => { this.handleError(error); this.versionsLoading.set(false); }
    });
  }

  protected formatSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} Ko` : `${(bytes / (1024 * 1024)).toLocaleString('fr-FR', { maximumFractionDigits: 1 })} Mio`;
  }

  protected statusLabel(status: string): string {
    return status === 'UploadedUnanalysed' ? 'Importé · non analysé' : status;
  }

  private writeSeries(request: () => ReturnType<DocumentsApiService['createSeries']>): void {
    this.savingSeries.set(true);
    this.error.set(null);
    request().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.savingSeries.set(false); this.load(); },
      error: (error: HttpErrorResponse) => { this.handleError(error); this.savingSeries.set(false); }
    });
  }

  private describeError(error: HttpErrorResponse): string {
    if (error.status === 401) return 'Accès refusé. Vérifiez votre session et les jetons de service configurés dans le Gateway.';
    if (error.status === 404) return 'Le document ou le dossier demandé est introuvable.';
    if (error.status === 409) return 'Ce CV appartient déjà à un dossier, ou cette opération contredit l’historique existant.';
    if (error.status === 413) return 'Le fichier dépasse la limite autorisée par le serveur.';
    if (error.status === 429) return 'La limite temporaire de requêtes Documents est atteinte. Réessayez plus tard.';
    return 'Le service Documents est indisponible. Vérifiez qu’il est démarré puis réessayez.';
  }

  private handleError(error: HttpErrorResponse): void {
    this.error.set(this.describeError(error));
  }
}

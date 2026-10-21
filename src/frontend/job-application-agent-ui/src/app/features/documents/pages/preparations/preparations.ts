import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, concatMap, forkJoin, map, of, throwError } from 'rxjs';
import { ApplicationsApiService } from '../../../applications/data-access/applications-api.service';
import { ProfileApiService } from '../../../profile/data-access/profile-api.service';
import { DocumentsApiService } from '../../data-access/documents-api.service';
import {
  ComposedDocument,
  DocumentExportMetadata,
  PreparedDraft,
  PreparedDraftSummary,
} from '../../models/document.models';
import { CompositionActions } from '../../components/composition-actions/composition-actions';

@Component({
  selector: 'app-preparations',
  imports: [DatePipe, CompositionActions, RouterLink],
  templateUrl: './preparations.html',
  styleUrl: './preparations.scss',
})
export class Preparations {
  private readonly api = inject(DocumentsApiService);
  private readonly applicationsApi = inject(ApplicationsApiService);
  private readonly profileApi = inject(ProfileApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(false);
  protected readonly detailLoading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly success = signal<string | null>(null);
  protected readonly drafts = signal<PreparedDraftSummary[]>([]);
  protected readonly selected = signal<PreparedDraft | null>(null);
  protected readonly composition = signal<ComposedDocument | null>(null);
  protected readonly exports = signal<DocumentExportMetadata[]>([]);
  protected readonly reviewConfirmed = signal(false);
  protected readonly reviewNote = signal('');
  protected readonly actionInProgress = signal(false);
  protected readonly applicationCreated = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api
      .listDrafts()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (drafts) => {
          this.drafts.set(drafts);
          this.loading.set(false);
        },
        error: (error) => {
          this.handleError(error);
          this.loading.set(false);
        },
      });
  }

  protected open(draft: PreparedDraftSummary): void {
    this.detailLoading.set(true);
    this.error.set(null);
    this.success.set(null);
    forkJoin({
      draft: this.api.getDraft(draft.id),
      composition: this.api
        .getComposition(draft.id)
        .pipe(catchError((error) => (error.status === 404 ? of(null) : throwError(() => error)))),
      exports: this.api.listExports(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.selected.set(result.draft);
          this.composition.set(result.composition);
          this.exports.set(result.exports);
          this.reviewConfirmed.set(false);
          this.reviewNote.set('');
          this.applicationCreated.set(false);
          this.detailLoading.set(false);
        },
        error: (error) => {
          this.handleError(error);
          this.detailLoading.set(false);
        },
      });
  }

  protected statusLabel(status: string): string {
    return status === 'PreparedForReview' ? 'À examiner' : status;
  }

  protected selectedExports(): DocumentExportMetadata[] {
    const draftId = this.selected()?.draft.id;
    return draftId ? this.exports().filter((item) => item.draftId === draftId) : [];
  }

  protected attachableExport(): DocumentExportMetadata | null {
    if (this.composition()?.composition.decision !== 'Approved') return null;
    const hash = this.composition()?.composition.contentSha256;
    const matching = this.selectedExports().filter((item) => item.compositionSha256 === hash);
    return matching.find((item) => item.format === 'Pdf') ?? matching[0] ?? null;
  }

  protected createApplication(): void {
    const draft = this.selected()?.draft;
    const file = this.attachableExport();
    if (!draft || !file || this.actionInProgress() || this.applicationCreated()) return;
    this.actionInProgress.set(true);
    this.error.set(null);
    this.success.set(null);
    this.profileApi
      .getProfile()
      .pipe(
        concatMap((profile) =>
          this.applicationsApi.create(crypto.randomUUID(), profile.id, draft.jobId),
        ),
        concatMap((created) =>
          this.applicationsApi.get(created.id).pipe(
            concatMap((application) =>
              this.applicationsApi.attachDocument(application.id, application.revision, file.id),
            ),
            map(() => created.id),
          ),
        ),
        concatMap((id) => this.applicationsApi.get(id)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (application) => {
          this.applicationCreated.set(true);
          this.actionInProgress.set(false);
          this.success.set(
            `Dossier prêt et export associé : ${application.jobTitle} — ${application.companyName}. Retrouvez-le dans Applications.`,
          );
        },
        error: (error) => {
          this.actionInProgress.set(false);
          this.handleError(error);
        },
      });
  }

  protected compose(): void {
    const draft = this.selected();
    if (!draft || this.actionInProgress()) return;
    this.actionInProgress.set(true);
    this.error.set(null);
    this.api
      .composeDraft(draft.draft.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.composition.set(result.document);
          this.actionInProgress.set(false);
        },
        error: (error) => {
          this.handleError(error);
          this.actionInProgress.set(false);
        },
      });
  }

  protected updateReviewConfirmed(confirmed: boolean): void {
    this.reviewConfirmed.set(confirmed);
  }

  protected updateReviewNote(note: string): void {
    this.reviewNote.set(note);
  }

  protected review(decision: 'Approved' | 'Rejected'): void {
    const draft = this.selected();
    const composition = this.composition();
    if (
      !draft ||
      !composition ||
      composition.composition.decision !== 'Pending' ||
      !this.reviewConfirmed() ||
      this.actionInProgress()
    )
      return;
    this.actionInProgress.set(true);
    this.error.set(null);
    this.api
      .reviewComposition(
        draft.draft.id,
        composition.composition.contentSha256,
        decision,
        true,
        this.reviewNote().trim() || null,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (updated) => {
          this.composition.set(updated);
          this.reviewConfirmed.set(false);
          this.actionInProgress.set(false);
        },
        error: (error) => {
          this.handleError(error);
          this.actionInProgress.set(false);
        },
      });
  }

  protected generateExport(format: 'Pdf' | 'Docx'): void {
    const draft = this.selected();
    const composition = this.composition();
    if (
      !draft ||
      !composition ||
      composition.composition.decision !== 'Approved' ||
      this.actionInProgress()
    )
      return;
    this.actionInProgress.set(true);
    this.error.set(null);
    this.api
      .createExport(draft.draft.id, format, composition.composition.contentSha256)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.exports.update((items) => [
            result.export,
            ...items.filter((item) => item.id !== result.export.id),
          ]);
          this.actionInProgress.set(false);
          this.downloadExport(result.export);
        },
        error: (error) => {
          this.handleError(error);
          this.actionInProgress.set(false);
        },
      });
  }

  protected downloadExport(file: DocumentExportMetadata): void {
    this.api
      .downloadExport(file.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (blob) => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = file.fileName;
          link.click();
          window.setTimeout(() => URL.revokeObjectURL(url), 1000);
        },
        error: (error) => this.handleError(error),
      });
  }

  private handleError(error: HttpErrorResponse): void {
    if (error.status === 401) {
      this.error.set(
        'Accès refusé. Vérifiez votre session et les autorisations des services dans le Gateway.',
      );
      return;
    }
    this.error.set(
      error.status === 404
        ? 'Ce dossier n’est pas disponible.'
        : error.status === 409
          ? 'Le dossier de candidature a changé. Actualisez Applications puis réessayez.'
          : error.status === 422
            ? 'Le profil, l’offre ou l’export ne peut pas être associé à cette candidature.'
            : 'Un service requis ne répond pas. Vérifiez son état puis réessayez.',
    );
  }
}

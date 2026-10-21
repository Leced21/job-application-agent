import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { concatMap, forkJoin } from 'rxjs';
import { DocumentsApiService } from '../../../documents/data-access/documents-api.service';
import {
  DocumentExportMetadata,
  PreparedDraftSummary,
} from '../../../documents/models/document.models';
import { ApplicationsApiService } from '../../data-access/applications-api.service';
import {
  ApplicationDetail,
  ApplicationStatus,
  ApplicationSummary,
  ContactChannel,
  DueFollowUp,
  SubmissionAttemptOutcome,
  SubmissionPreview,
} from '../../models/application.models';

interface AttachCandidateExport {
  file: DocumentExportMetadata;
  kind: 'Resume' | 'CoverLetter';
}

@Component({
  selector: 'app-applications',
  imports: [DatePipe, RouterLink],
  templateUrl: './applications.html',
  styleUrl: './applications.scss',
})
export class Applications {
  private readonly api = inject(ApplicationsApiService);
  private readonly documentsApi = inject(DocumentsApiService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(false);
  protected readonly detailLoading = signal(false);
  protected readonly saving = signal(false);
  protected readonly approvalConfirmedValue = signal(false);
  protected readonly returnDraftConfirmedValue = signal(false);
  protected readonly withdrawReasonValue = signal('');
  protected readonly withdrawConfirmedValue = signal(false);
  protected readonly submissionDetailsReviewed = signal(false);
  protected readonly submissionDocumentsReviewed = signal(false);
  protected readonly submissionPreview = signal<SubmissionPreview | null>(null);
  protected readonly submissionPlatformIds = signal<string[]>([]);
  protected readonly submissionAutomationEnabled = signal(false);
  protected readonly submissionPlatformId = signal('');
  protected readonly controlledApprovalConfirmed = signal(false);
  protected readonly controlledAttemptConfirmed = signal(false);
  protected readonly controlledResultConfirmed = signal(false);
  private reservationAttemptId: string | null = null;
  protected readonly error = signal<string | null>(null);
  protected readonly applications = signal<ApplicationSummary[]>([]);
  protected readonly dueItems = signal<DueFollowUp[]>([]);
  protected readonly selected = signal<ApplicationDetail | null>(null);
  protected readonly availableExports = signal<AttachCandidateExport[]>([]);
  protected readonly selectedExportId = signal('');
  protected readonly exportsLoading = signal(false);
  protected readonly previewFile = signal<DocumentExportMetadata | null>(null);
  protected readonly previewUrl = signal<SafeResourceUrl | null>(null);
  protected readonly previewLoadingId = signal<string | null>(null);
  protected readonly followUpDateMin = this.localDateTime(
    new Date(Date.now() + 60_000).toISOString(),
  );
  protected readonly followUpDateMax = this.localDateTime(
    new Date(Date.now() + 365 * 24 * 60 * 60 * 1000).toISOString(),
  );
  private previewObjectUrl: string | null = null;
  protected readonly filter = signal<ApplicationStatus | null>(null);
  protected readonly statuses: ApplicationStatus[] = [
    'Draft',
    'ReadyForReview',
    'Approved',
    'Submitted',
    'InReview',
    'Interview',
    'Offer',
    'Rejected',
    'Withdrawn',
  ];
  protected readonly channels: ContactChannel[] = [
    'Email',
    'JobBoard',
    'CompanyWebsite',
    'Phone',
    'InPerson',
    'Other',
  ];

  constructor() {
    this.destroyRef.onDestroy(() => this.clearPreview());
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({ applications: this.api.list(this.filter()), due: this.api.dueFollowUps() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (result) => {
          this.applications.set(result.applications.items);
          this.dueItems.set(result.due.items);
          this.loading.set(false);
        },
        error: (error) => {
          this.handleError(error);
          this.loading.set(false);
        },
      });
  }

  protected changeFilter(value: string): void {
    this.filter.set(value ? (value as ApplicationStatus) : null);
    this.load();
  }

  protected open(id: string): void {
    this.detailLoading.set(true);
    this.error.set(null);
    this.api
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (value) => {
          this.selected.set(value);
          this.approvalConfirmedValue.set(false);
          this.returnDraftConfirmedValue.set(false);
          this.withdrawReasonValue.set('');
          this.withdrawConfirmedValue.set(false);
          this.submissionDetailsReviewed.set(false);
          this.submissionDocumentsReviewed.set(false);
          this.submissionPreview.set(null);
          this.submissionPlatformIds.set([]);
          this.submissionAutomationEnabled.set(false);
          this.submissionPlatformId.set('');
          this.controlledApprovalConfirmed.set(false);
          this.controlledAttemptConfirmed.set(false);
          this.controlledResultConfirmed.set(false);
          this.reservationAttemptId =
            value.submissionAttemptResult?.outcome === 'DefinitelyNotSubmitted'
              ? null
              : (value.submissionAttempt?.attemptId ?? null);
          this.availableExports.set([]);
          this.selectedExportId.set('');
          this.clearPreview();
          this.detailLoading.set(false);
          if (value.status === 'Draft' || value.resume || value.coverLetter)
            this.loadAttachableExports(value);
          if (value.status === 'Approved' || value.submissionAttempt)
            this.loadControlledSubmissionOptions(value.id, value.status === 'Approved');
        },
        error: (error) => {
          this.handleError(error);
          this.detailLoading.set(false);
        },
      });
  }

  protected attachExport(exportId: string): void {
    const current = this.selected();
    if (!current || current.status !== 'Draft' || !exportId || this.saving()) return;
    this.saving.set(true);
    this.error.set(null);
    this.api
      .attachDocument(current.id, current.revision, exportId)
      .pipe(
        concatMap(() => this.api.get(current.id)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (application) => {
          this.selected.set(application);
          this.selectedExportId.set('');
          this.saving.set(false);
          this.load();
        },
        error: (error) => {
          this.handleError(error);
          this.saving.set(false);
        },
      });
  }

  protected exportLabel(item: AttachCandidateExport): string {
    return `${item.kind === 'Resume' ? 'CV' : 'Lettre'} · ${item.file.fileName} · ${item.file.format}`;
  }

  protected isExportAttached(item: AttachCandidateExport): boolean {
    const current = this.selected();
    return item.kind === 'Resume'
      ? current?.resume?.exportId === item.file.id
      : current?.coverLetter?.exportId === item.file.id;
  }

  protected exportById(id: string): DocumentExportMetadata | null {
    return this.availableExports().find((item) => item.file.id === id)?.file ?? null;
  }

  protected isPdf(file: DocumentExportMetadata): boolean {
    return file.mediaType.toLowerCase().startsWith('application/pdf');
  }

  protected previewExport(file: DocumentExportMetadata): void {
    if (!this.isPdf(file)) {
      this.error.set(
        'Seuls les fichiers PDF peuvent être prévisualisés. Vous pouvez télécharger cette pièce.',
      );
      return;
    }
    this.clearPreview();
    this.previewLoadingId.set(file.id);
    this.error.set(null);
    this.documentsApi
      .downloadExport(file.id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (blob) => {
          if (blob.type && !blob.type.toLowerCase().startsWith('application/pdf')) {
            this.error.set('Le service Documents n’a pas renvoyé un fichier PDF.');
            this.previewLoadingId.set(null);
            return;
          }
          this.previewObjectUrl = URL.createObjectURL(blob);
          this.previewFile.set(file);
          this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(this.previewObjectUrl));
          this.previewLoadingId.set(null);
        },
        error: (error) => {
          this.previewLoadingId.set(null);
          this.handleError(error);
        },
      });
  }

  protected downloadExport(file: DocumentExportMetadata): void {
    this.documentsApi
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

  protected closePreview(): void {
    this.clearPreview();
  }

  private clearPreview(): void {
    if (this.previewObjectUrl) URL.revokeObjectURL(this.previewObjectUrl);
    this.previewObjectUrl = null;
    this.previewFile.set(null);
    this.previewUrl.set(null);
    this.previewLoadingId.set(null);
  }

  private loadAttachableExports(application: ApplicationDetail): void {
    this.exportsLoading.set(true);
    forkJoin({ drafts: this.documentsApi.listDrafts(), exports: this.documentsApi.listExports() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ drafts, exports }) => {
          const draftById = new Map<string, PreparedDraftSummary>(
            drafts
              .filter((draft) => draft.jobId === application.jobId)
              .map((draft) => [draft.id, draft]),
          );
          this.availableExports.set(
            exports.flatMap((file) => {
              const draft = draftById.get(file.draftId);
              return draft && file.approvedAtUtc ? [{ file, kind: draft.kind }] : [];
            }),
          );
          this.exportsLoading.set(false);
        },
        error: (error) => {
          this.exportsLoading.set(false);
          this.handleError(error);
        },
      });
  }

  protected saveNote(note: string): void {
    const current = this.selected();
    if (!current || !note.trim() || this.saving()) return;
    this.mutate(this.api.addNote(current.id, current.revision, note.trim()));
  }

  protected requestReview(): void {
    const current = this.selected();
    if (
      !current ||
      current.status !== 'Draft' ||
      !current.resume ||
      !current.coverLetter ||
      this.saving()
    ) {
      this.error.set('Associez un CV et une lettre approuvés avant de demander la revue.');
      return;
    }
    this.mutate(this.api.requestReview(current.id, current.revision));
  }

  protected approveApplication(confirmed: boolean): void {
    const current = this.selected();
    if (
      !current ||
      current.status !== 'ReadyForReview' ||
      !current.resume ||
      !current.coverLetter ||
      !confirmed ||
      this.saving()
    ) {
      this.error.set(
        'Relisez les deux pièces et confirmez explicitement l’approbation du dossier.',
      );
      return;
    }
    this.mutate(this.api.approve(current.id, current.revision, true));
  }

  protected returnToDraft(confirmed: boolean): void {
    const current = this.selected();
    if (
      !current ||
      !['ReadyForReview', 'Approved'].includes(current.status) ||
      !confirmed ||
      this.saving()
    ) {
      this.error.set('Confirmez le retour au brouillon pour modifier les pièces de ce dossier.');
      return;
    }
    this.mutate(this.api.returnToDraft(current.id, current.revision));
  }

  protected canWithdraw(status: ApplicationStatus): boolean {
    return !['Withdrawn', 'Offer', 'Rejected'].includes(status);
  }

  protected withdraw(reason: string, confirmed: boolean): void {
    const current = this.selected();
    if (
      !current ||
      !this.canWithdraw(current.status) ||
      !reason.trim() ||
      !confirmed ||
      this.saving()
    ) {
      this.error.set('Indiquez un motif et confirmez le retrait de cette candidature.');
      return;
    }
    this.mutate(this.api.withdraw(current.id, current.revision, reason.trim()));
  }

  protected planFollowUp(dueAtLocal: string, channel: string, note: string): void {
    const current = this.selected();
    const due = new Date(dueAtLocal);
    if (
      !current ||
      !dueAtLocal ||
      !Number.isFinite(due.getTime()) ||
      !this.isValidFollowUpDate(due) ||
      !note.trim() ||
      this.saving()
    ) {
      this.error.set('Choisissez une échéance future dans les 365 jours et indiquez une note.');
      return;
    }
    this.mutate(
      this.api.planFollowUp(
        current.id,
        current.revision,
        due.toISOString(),
        channel as ContactChannel,
        note.trim(),
      ),
    );
  }

  protected rescheduleFollowUp(dueAtLocal: string, channel: string, note: string): void {
    const current = this.selected();
    const pending = current?.pendingFollowUp;
    const due = new Date(dueAtLocal);
    if (
      !current ||
      !pending ||
      !dueAtLocal ||
      !Number.isFinite(due.getTime()) ||
      !this.isValidFollowUpDate(due) ||
      !note.trim() ||
      this.saving()
    ) {
      this.error.set('Choisissez une échéance future dans les 365 jours et indiquez une note.');
      return;
    }
    this.mutate(
      this.api.rescheduleFollowUp(
        current.id,
        pending.plan.id,
        current.revision,
        due.toISOString(),
        channel as ContactChannel,
        note.trim(),
      ),
    );
  }

  protected localDateTime(value: string): string {
    const date = new Date(value);
    if (!Number.isFinite(date.getTime())) return '';
    return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
  }

  private isValidFollowUpDate(value: Date): boolean {
    const now = Date.now();
    const due = value.getTime();
    return due >= now + 60_000 && due <= now + 365 * 24 * 60 * 60 * 1000;
  }

  protected declareSubmission(
    occurredAtLocal: string,
    channel: string,
    reference: string,
    confirmed: boolean,
  ): void {
    const current = this.selected();
    const occurredAt = new Date(occurredAtLocal);
    if (
      !current ||
      current.status !== 'Approved' ||
      current.submission ||
      !confirmed ||
      !occurredAtLocal ||
      !Number.isFinite(occurredAt.getTime()) ||
      this.saving()
    ) {
      this.error.set('Confirmez l’envoi et indiquez sa date pour l’enregistrer.');
      return;
    }
    this.mutate(
      this.api.declareManualSubmission(
        current.id,
        current.revision,
        occurredAt.toISOString(),
        channel as ContactChannel,
        reference.trim() || null,
      ),
    );
  }

  private loadControlledSubmissionOptions(id: string, loadPreview: boolean): void {
    this.api
      .submissionPlatforms()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (options) => {
          if (this.selected()?.id !== id) return;
          this.submissionPlatformIds.set(options.platformIds);
          this.submissionAutomationEnabled.set(options.externalSubmissionEnabled);
          if (!this.submissionPlatformId() && options.platformIds.length)
            this.submissionPlatformId.set(options.platformIds[0]);
        },
        error: (error) => {
          if (this.selected()?.id === id) this.handleError(error);
        },
      });
    if (!loadPreview) return;
    this.api
      .submissionPreview(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (preview) => {
          if (this.selected()?.id === id) this.submissionPreview.set(preview);
        },
        error: (error) => {
          // A conflict is expected while a reserved attempt awaits its reported outcome.
          if (error.status !== 409 && this.selected()?.id === id) this.handleError(error);
        },
      });
  }

  protected approveControlledSubmission(confirmed: boolean): void {
    const current = this.selected();
    const preview = this.submissionPreview();
    if (!current || !preview || !confirmed || this.saving()) {
      this.error.set('Relisez l’aperçu et confirmez explicitement l’accord avant de continuer.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.api
      .approveControlledSubmission(current.id, preview.revision, preview.reviewSha256)
      .pipe(concatMap(() => this.api.get(current.id)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (application) => {
          this.selected.set(application);
          this.controlledApprovalConfirmed.set(false);
          this.saving.set(false);
          this.loadControlledSubmissionOptions(application.id, true);
          this.load();
        },
        error: (error) => {
          this.handleError(error);
          this.saving.set(false);
        },
      });
  }

  protected prepareControlledSubmissionAttempt(confirmed: boolean): void {
    const current = this.selected();
    const preview = this.submissionPreview();
    const platformId = this.submissionPlatformId();
    if (
      !current ||
      !preview ||
      !current.submissionApproval ||
      !platformId ||
      !this.submissionAutomationEnabled() ||
      !confirmed ||
      this.saving()
    ) {
      this.error.set('La réservation exige un accord valide, une plateforme autorisée et une confirmation.');
      return;
    }
    this.reservationAttemptId ??= crypto.randomUUID();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .prepareSubmissionAttempt(
        current.id,
        preview.revision,
        this.reservationAttemptId,
        platformId,
        preview.reviewSha256,
      )
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (application) => {
          this.selected.set(application);
          this.submissionPreview.set(null);
          this.controlledAttemptConfirmed.set(false);
          this.saving.set(false);
          this.load();
        },
        error: (error) => {
          this.handleError(error);
          this.saving.set(false);
        },
      });
  }

  protected recordControlledSubmissionResult(
    outcome: string,
    occurredAtLocal: string,
    reference: string,
    confirmed: boolean,
  ): void {
    const current = this.selected();
    const attempt = current?.submissionAttempt;
    const occurredAt = new Date(occurredAtLocal);
    if (
      !current ||
      !attempt ||
      current.submissionAttemptResult ||
      !['Submitted', 'DefinitelyNotSubmitted', 'Unknown'].includes(outcome) ||
      !occurredAtLocal ||
      !Number.isFinite(occurredAt.getTime()) ||
      occurredAt.getTime() < new Date(attempt.preparedAtUtc).getTime() ||
      occurredAt.getTime() > Date.now() ||
      !confirmed ||
      this.saving()
    ) {
      this.error.set('Confirmez le résultat réel de la tentative et indiquez une date valide.');
      return;
    }
    const result = outcome as SubmissionAttemptOutcome;
    this.mutate(
      this.api.recordSubmissionAttemptResult(
        current.id,
        current.revision,
        attempt.attemptId,
        result,
        occurredAt.toISOString(),
        result === 'Submitted' ? reference.trim() || null : null,
      ),
    );
  }

  protected recruiterTargets(): ApplicationStatus[] {
    switch (this.selected()?.status) {
      case 'Submitted':
        return ['Submitted', 'InReview', 'Interview', 'Offer', 'Rejected'];
      case 'InReview':
        return ['InReview', 'Interview', 'Offer', 'Rejected'];
      case 'Interview':
        return ['Interview', 'Offer', 'Rejected'];
      default:
        return [];
    }
  }

  protected recordRecruiterUpdate(
    status: string,
    occurredAtLocal: string,
    channel: string,
    message: string,
    confirmed: boolean,
  ): void {
    const current = this.selected();
    const occurredAt = new Date(occurredAtLocal);
    if (
      !current ||
      !this.recruiterTargets().length ||
      !confirmed ||
      !occurredAtLocal ||
      !Number.isFinite(occurredAt.getTime()) ||
      !message.trim() ||
      this.saving()
    ) {
      this.error.set('Confirmez le retour reçu et renseignez sa date et son contenu.');
      return;
    }
    this.mutate(
      this.api.recordRecruiterUpdate(
        current.id,
        current.revision,
        status as ApplicationStatus,
        occurredAt.toISOString(),
        channel as ContactChannel,
        message.trim(),
      ),
    );
  }

  protected completeFollowUp(
    occurredAtLocal: string,
    channel: string,
    outcome: string,
    confirmed: boolean,
  ): void {
    const current = this.selected();
    const pending = current?.pendingFollowUp;
    const occurredAt = new Date(occurredAtLocal);
    if (
      !current ||
      !pending ||
      !confirmed ||
      !occurredAtLocal ||
      !Number.isFinite(occurredAt.getTime()) ||
      !outcome.trim() ||
      this.saving()
    ) {
      this.error.set('Confirmez la relance effectuée et renseignez sa date et son résultat.');
      return;
    }
    this.mutate(
      this.api.completeFollowUp(
        current.id,
        pending.plan.id,
        current.revision,
        occurredAt.toISOString(),
        channel as ContactChannel,
        outcome.trim(),
      ),
    );
  }

  protected cancelFollowUp(reason: string, confirmed: boolean): void {
    const current = this.selected();
    const pending = current?.pendingFollowUp;
    if (!current || !pending || !confirmed || !reason.trim() || this.saving()) {
      this.error.set('Confirmez l’annulation de la relance et indiquez un motif.');
      return;
    }
    this.mutate(
      this.api.cancelFollowUp(current.id, pending.plan.id, current.revision, reason.trim()),
    );
  }

  protected statusLabel(status: ApplicationStatus): string {
    const labels: Record<ApplicationStatus, string> = {
      Draft: 'Brouillon',
      ReadyForReview: 'À relire',
      Approved: 'Approuvée',
      Withdrawn: 'Retirée',
      Submitted: 'Envoyée manuellement',
      InReview: 'En cours',
      Interview: 'Entretien',
      Offer: 'Offre reçue',
      Rejected: 'Refusée',
    };
    return labels[status];
  }

  protected channelLabel(channel: ContactChannel): string {
    const labels: Record<ContactChannel, string> = {
      Email: 'Courriel',
      JobBoard: 'Site d’emploi',
      CompanyWebsite: 'Site entreprise',
      Phone: 'Téléphone',
      InPerson: 'En personne',
      Other: 'Autre',
    };
    return labels[channel];
  }

  protected controlledOutcomeLabel(outcome: SubmissionAttemptOutcome): string {
    const labels: Record<SubmissionAttemptOutcome, string> = {
      Submitted: 'Envoi confirmé',
      DefinitelyNotSubmitted: 'Aucun envoi effectué',
      Unknown: 'Résultat incertain',
    };
    return labels[outcome];
  }

  protected eventLabel(kind: string): string {
    const labels: Record<string, string> = {
      Created: 'Dossier créé',
      DocumentAttached: 'Document associé',
      ReviewRequested: 'Revue demandée',
      Approved: 'Dossier approuvé',
      ReturnedToDraft: 'Renvoyé au brouillon',
      NoteAdded: 'Note ajoutée',
      ManualSubmissionDeclared: 'Envoi manuel déclaré',
      RecruiterUpdateRecorded: 'Retour recruteur enregistré',
      FollowUpPlanned: 'Relance planifiée',
      FollowUpRescheduled: 'Relance replanifiée',
      FollowUpCompleted: 'Relance effectuée',
      FollowUpCancelled: 'Relance annulée',
      Withdrawn: 'Candidature retirée',
      ControlledSubmissionApproved: 'Accord de soumission enregistré',
      ControlledSubmissionAttemptPrepared: 'Tentative réservée',
      ControlledSubmissionResultRecorded: 'Résultat de tentative enregistré',
    };
    return labels[kind] ?? kind;
  }

  protected onDueClick(id: string): void {
    this.open(id);
  }

  private mutate(request: ReturnType<ApplicationsApiService['addNote']>): void {
    this.saving.set(true);
    this.error.set(null);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (value) => {
        this.selected.set(value);
        this.reservationAttemptId =
          value.submissionAttemptResult?.outcome === 'DefinitelyNotSubmitted'
            ? null
            : (value.submissionAttempt?.attemptId ?? null);
        this.saving.set(false);
        this.load();
        if (value.status === 'Draft') this.loadAttachableExports(value);
        if (value.status === 'Approved') this.loadControlledSubmissionOptions(value.id, true);
      },
      error: (error) => {
        this.handleError(error);
        this.saving.set(false);
      },
    });
  }

  private handleError(error: HttpErrorResponse): void {
    if (error.status === 401) {
      this.error.set(
        'Accès refusé. Vérifiez votre session et les autorisations Applications et Documents dans le Gateway.',
      );
      return;
    }
    this.error.set(
      error.status === 409
        ? 'Le dossier a changé depuis sa lecture. Actualisez-le avant de réessayer.'
        : error.status === 429
          ? 'La limite de requêtes est atteinte. Réessayez plus tard.'
          : error.status === 400
            ? 'Les données saisies sont invalides. Vérifiez les champs et les règles du dossier.'
            : 'Un service requis ne répond pas. Vérifiez Applications et Documents puis réessayez.',
    );
  }
}

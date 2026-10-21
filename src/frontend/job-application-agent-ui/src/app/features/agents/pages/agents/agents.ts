import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { AgentsApiService } from '../../data-access/agents-api.service';
import {
  AgentBudget,
  AgentCatalog,
  AgentKind,
  AgentResourceOption,
  AgentRun,
  AgentRunSummary,
} from '../../models/agents.models';

@Component({
  selector: 'app-agents',
  imports: [CurrencyPipe, DatePipe, DecimalPipe],
  templateUrl: './agents.html',
  styleUrl: './agents.scss',
})
export class Agents {
  private readonly api = inject(AgentsApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly loading = signal(false);
  protected readonly loadingResources = signal(false);
  protected readonly running = signal(false);
  protected readonly loadingHistory = signal(false);
  protected readonly reviewing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly historyError = signal<string | null>(null);
  protected readonly feedback = signal<string | null>(null);
  protected readonly catalog = signal<AgentCatalog | null>(null);
  protected readonly budget = signal<AgentBudget | null>(null);
  protected readonly kind = signal<AgentKind>('ProfileAnalysis');
  protected readonly resources = signal<AgentResourceOption[]>([]);
  protected readonly resourceId = signal('');
  protected readonly runs = signal<AgentRunSummary[]>([]);
  protected readonly selectedRun = signal<AgentRun | null>(null);
  protected readonly reviewConfirmed = signal(false);
  protected readonly reviewNote = signal('');
  protected readonly agentKinds: AgentKind[] = [
    'ProfileAnalysis', 'JobAnalysis', 'MatchingEnrichment', 'ResumeAdaptation',
    'CoverLetter', 'ApplicationPreparation', 'FollowUpSuggestion',
  ];

  constructor() {
    this.load();
    this.loadResources(this.kind());
    this.loadRuns();
  }

  protected load(): void {
    this.loading.set(true);
    this.error.set(null);
    forkJoin({ catalog: this.api.catalog(), budget: this.api.budget() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: value => {
          this.catalog.set(value.catalog);
          this.budget.set(value.budget);
          this.loading.set(false);
        },
        error: error => {
          this.handleError(error);
          this.loading.set(false);
        },
      });
  }

  protected selectKind(value: string): void {
    if (!this.agentKinds.includes(value as AgentKind)) return;
    const kind = value as AgentKind;
    this.kind.set(kind);
    this.selectedRun.set(null);
    this.feedback.set(null);
    this.loadResources(kind);
  }

  protected selectResource(value: string): void {
    this.resourceId.set(value);
  }

  protected loadResources(kind: AgentKind): void {
    this.loadingResources.set(true);
    this.resources.set([]);
    this.resourceId.set('');
    this.api.resources(kind)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: resources => {
          this.resources.set(resources);
          this.resourceId.set(resources[0]?.id ?? '');
          this.loadingResources.set(false);
        },
        error: error => {
          this.loadingResources.set(false);
          this.feedback.set(this.resourceError(error));
        },
      });
  }

  protected execute(): void {
    const resourceId = this.resourceId();
    const kind = this.kind();
    if (!resourceId || this.running() || !this.enabled(kind, this.catalog())) return;
    this.running.set(true);
    this.feedback.set(null);
    this.error.set(null);
    this.api.execute({ requestId: crypto.randomUUID(), kind, resourceId })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: run => {
          this.running.set(false);
          this.selectedRun.set(run);
          this.reviewConfirmed.set(false);
          this.reviewNote.set('');
          this.feedback.set(run.state === 'PendingReview'
            ? 'La proposition est prête. Vérifie son contenu avant de l’approuver ou de la rejeter.'
            : run.state === 'Failed'
              ? this.failureLabel(run.failure)
              : 'Traitement terminé.');
          this.loadRuns();
          this.refreshBudget();
        },
        error: error => {
          this.running.set(false);
          this.handleError(error);
        },
      });
  }

  protected loadRuns(): void {
    this.loadingHistory.set(true);
    this.historyError.set(null);
    this.api.listRuns()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: runs => {
          this.runs.set(runs);
          this.loadingHistory.set(false);
        },
        error: error => {
          this.historyError.set(this.resourceError(error));
          this.loadingHistory.set(false);
        },
      });
  }

  protected openRun(id: string): void {
    this.feedback.set(null);
    this.api.getRun(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: run => {
          this.selectedRun.set(run);
          this.reviewConfirmed.set(false);
          this.reviewNote.set('');
        },
        error: error => this.handleError(error),
      });
  }

  protected review(approved: boolean): void {
    const run = this.selectedRun();
    if (!run?.proposal || run.state !== 'PendingReview' || this.reviewing()) return;
    if (!this.reviewConfirmed()) {
      this.feedback.set('Coche la confirmation après avoir lu toute la proposition.');
      return;
    }
    this.reviewing.set(true);
    this.feedback.set(null);
    this.api.review(run.id, {
      expectedRevision: run.revision,
      outputSha256: run.proposal.outputSha256,
      confirmed: true,
      approved,
      note: this.reviewNote().trim() || null,
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: reviewed => {
          this.selectedRun.set(reviewed);
          this.reviewing.set(false);
          this.reviewConfirmed.set(false);
          this.feedback.set(approved
            ? 'Proposition approuvée et conservée dans l’historique.'
            : 'Proposition rejetée et conservée dans l’historique.');
          this.loadRuns();
          this.refreshBudget();
        },
        error: error => {
          this.reviewing.set(false);
          this.handleError(error);
        },
      });
  }

  protected setReviewConfirmed(value: boolean): void {
    this.reviewConfirmed.set(value);
  }

  protected setReviewNote(value: string): void {
    this.reviewNote.set(value);
  }

  protected enabled(kind: string, catalog: AgentCatalog | null): boolean {
    if (!catalog) return false;
    switch (kind) {
      case 'JobAnalysis': return catalog.modelAvailable;
      case 'ProfileAnalysis': return catalog.profileAnalysisEnabled;
      case 'MatchingEnrichment': return catalog.matchingEnrichmentEnabled;
      case 'ResumeAdaptation': return catalog.resumeAdaptationEnabled;
      case 'CoverLetter': return catalog.coverLetterEnabled;
      case 'ApplicationPreparation': return catalog.applicationPreparationEnabled;
      case 'FollowUpSuggestion': return catalog.followUpSuggestionEnabled;
      default: return false;
    }
  }

  protected statusLabel(kind: string, catalog: AgentCatalog): string {
    return this.enabled(kind, catalog) ? 'Activé' : 'Désactivé';
  }

  protected kindLabel(kind: string): string {
    const labels: Record<string, string> = {
      ProfileAnalysis: 'Analyse du profil', JobAnalysis: 'Analyse d’offre', MatchingEnrichment: 'Enrichissement du matching',
      ResumeAdaptation: 'Adaptation du CV', CoverLetter: 'Lettre de motivation',
      ApplicationPreparation: 'Préparation de candidature', FollowUpSuggestion: 'Suggestion de relance',
    };
    return labels[kind] ?? kind;
  }

  protected stateLabel(state: string): string {
    const labels: Record<string, string> = {
      Running: 'En cours', PendingReview: 'À examiner', Approved: 'Approuvée', Rejected: 'Rejetée', Failed: 'Échec',
    };
    return labels[state] ?? state;
  }

  protected kindHelp(kind: AgentKind): string {
    switch (kind) {
      case 'ProfileAnalysis': return 'Repère les rubriques manquantes du profil. La proposition ne modifie pas le profil.';
      case 'JobAnalysis': return 'Extrait les critères d’une offre enregistrée. Une analyse approuvée est requise avant les agents CV et lettre.';
      case 'MatchingEnrichment': return 'Examine un résultat de matching terminé et propose les points à vérifier.';
      case 'ResumeAdaptation': return 'Prépare un brouillon de CV à partir d’une analyse d’offre préalablement approuvée.';
      case 'CoverLetter': return 'Prépare des paragraphes de lettre à partir d’une analyse d’offre préalablement approuvée.';
      case 'ApplicationPreparation': return 'Évalue les éléments disponibles pour préparer la candidature.';
      case 'FollowUpSuggestion': return 'Suggère une relance pour une candidature soumise ou en cours de suivi.';
    }
  }

  protected failureLabel(failure: string | null): string {
    const labels: Record<string, string> = {
      UnsupportedAgent: 'Cet agent ne prend pas en charge ce traitement.',
      SourceRejected: 'La ressource ne contient pas les informations nécessaires ou n’est pas accessible.',
      InputLimit: 'Les informations dépassent la limite acceptée par le modèle.',
      BudgetRejected: 'Le budget disponible ne permet pas ce traitement.',
      ModelUnavailable: 'Le modèle local ne répond pas. Vérifie le service Ollama.',
      TimedOut: 'Le traitement a dépassé le délai autorisé.',
      Cancelled: 'Le traitement a été annulé.',
      InvalidOutput: 'Le modèle a produit une réponse qui ne respecte pas le format attendu.',
      UsageViolation: 'La réponse du modèle ne respecte pas les limites configurées.',
      SourceUnavailable: 'Une ressource nécessaire est momentanément indisponible.',
      BudgetUnavailable: 'Le service de budget est momentanément indisponible.',
      Interrupted: 'Le traitement a été interrompu avant sa finalisation.',
    };
    return labels[failure ?? ''] ?? 'Le traitement a échoué. Réessaie après avoir vérifié les services.';
  }

  protected prettyOutput(output: string): string {
    try { return JSON.stringify(JSON.parse(output), null, 2); }
    catch { return output; }
  }

  private refreshBudget(): void {
    this.api.budget().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: budget => this.budget.set(budget),
    });
  }

  private resourceError(error: HttpErrorResponse): string {
    if (error.status === 401) return 'Session expirée. Reconnecte-toi puis réessaie.';
    if (error.status === 404) return 'Aucune ressource compatible trouvée pour cet agent.';
    if (error.status === 400) return 'Le service a refusé la demande. Vérifie les paramètres du service concerné.';
    if (error.status === 429) return 'La limite de requêtes est atteinte. Réessaie plus tard.';
    return 'Impossible de charger les ressources ou l’historique. Vérifie les services puis réessaie.';
  }

  private handleError(error: HttpErrorResponse): void {
    this.error.set(this.resourceError(error));
  }
}

export interface AgentDefinition {
  kind: string;
  purpose: string;
  promptVersion: string | null;
  executable: boolean;
}

export interface AgentCatalog {
  definitions: AgentDefinition[];
  matchingEnrichmentEnabled: boolean;
  resumeAdaptationEnabled: boolean;
  coverLetterEnabled: boolean;
  applicationPreparationEnabled: boolean;
  followUpSuggestionEnabled: boolean;
  profileAnalysisEnabled: boolean;
  modelAvailable: boolean;
  provider: string;
  modelId: string | null;
  availabilityVerified: boolean;
  tokenizerProfile: string | null;
  tokenizerSha256: string | null;
  inputLimit: number;
  outputLimit: number;
}

export interface AgentBudget {
  maxTokens: number;
  maxUsd: number;
  chargedTokens: number;
  chargedUsd: number;
  heldTokens: number;
  heldUsd: number;
}

export type AgentKind =
  | 'ProfileAnalysis'
  | 'JobAnalysis'
  | 'MatchingEnrichment'
  | 'ResumeAdaptation'
  | 'CoverLetter'
  | 'ApplicationPreparation'
  | 'FollowUpSuggestion';

export type AgentRunState = 'Running' | 'PendingReview' | 'Approved' | 'Rejected' | 'Failed';

export interface AgentResourceOption {
  id: string;
  label: string;
}

export interface AgentRunCommand {
  requestId: string;
  kind: AgentKind;
  resourceId: string;
}

export interface AgentRunSummary {
  id: string;
  requestId: string;
  kind: AgentKind;
  state: AgentRunState;
  revision: number;
  createdAtUtc: string;
}

export interface AgentRun {
  id: string;
  command: AgentRunCommand;
  state: AgentRunState;
  revision: number;
  proposal: {
    modelId: string;
    promptVersion: string;
    outputJson: string;
    outputSha256: string;
  } | null;
  failure: string | null;
  accounting: {
    chargedTokens: number;
    chargedUsd: number;
    usageKnown: boolean;
  };
  reviewNote: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AgentReviewCommand {
  expectedRevision: number;
  outputSha256: string;
  confirmed: true;
  approved: boolean;
  note: string | null;
}

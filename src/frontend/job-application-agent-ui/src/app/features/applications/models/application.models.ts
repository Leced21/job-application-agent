export type ApplicationStatus = 'Draft' | 'ReadyForReview' | 'Approved' | 'Withdrawn' | 'Submitted' | 'InReview' | 'Interview' | 'Offer' | 'Rejected';
export type ContactChannel = 'Email' | 'JobBoard' | 'CompanyWebsite' | 'Phone' | 'InPerson' | 'Other';

export interface ApplicationSummary {
  id: string;
  profileId: string;
  jobId: string;
  jobTitle: string;
  companyName: string;
  status: ApplicationStatus;
  revision: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ApplicationDocument {
  exportId: string;
  kind: 'Resume' | 'CoverLetter';
  sha256: string;
}

export interface ApplicationEvent {
  sequence: number;
  kind: string;
  status: ApplicationStatus;
  revision: number;
  atUtc: string;
  note: string | null;
  document: ApplicationDocument | null;
  submission: { occurredAtUtc: string; channel: ContactChannel; reference: string | null } | null;
  recruiterUpdate: { occurredAtUtc: string; channel: ContactChannel; message: string } | null;
  followUp: FollowUpChange | null;
  submissionApproval?: SubmissionApproval | null;
  submissionAttempt?: SubmissionAttempt | null;
  submissionResult?: SubmissionAttemptResult | null;
}

export interface SubmissionApproval {
  candidacyId: string;
  revision: number;
  reviewSha256: string;
  confirmed: boolean;
}

export interface SubmissionPlatform {
  platformId: string;
  hasOfficialApi: boolean;
  termsAllowAutomation: boolean;
  requiresCaptcha: boolean;
  supportsIdempotencyKey: boolean;
}

export interface SubmissionAttempt {
  attemptId: string;
  approvedRevision: number;
  reviewSha256: string;
  platform: SubmissionPlatform;
  preparedAtUtc: string;
}

export type SubmissionAttemptOutcome = 'Submitted' | 'DefinitelyNotSubmitted' | 'Unknown';

export interface SubmissionAttemptResult {
  attemptId: string;
  outcome: SubmissionAttemptOutcome;
  occurredAtUtc: string;
  reference: string | null;
}

export interface SubmissionPreview {
  id: string;
  revision: number;
  jobTitle: string;
  companyName: string;
  resume: { exportId: string; sha256: string };
  coverLetter: { exportId: string; sha256: string };
  reviewSha256: string;
  externalSubmissionEnabled: boolean;
}

export interface SubmissionPlatformOptions {
  externalSubmissionEnabled: boolean;
  platformIds: string[];
}

export interface FollowUpPlan {
  id: string;
  dueAtUtc: string;
  channel: ContactChannel;
  note: string;
}

export interface FollowUpChange {
  plan: FollowUpPlan;
  status: 'Planned' | 'Completed' | 'Cancelled';
  occurredAtUtc: string | null;
  actualChannel: ContactChannel | null;
  outcome: string | null;
}

export interface ApplicationDetail extends ApplicationSummary {
  resume: ApplicationDocument | null;
  coverLetter: ApplicationDocument | null;
  submission: { occurredAtUtc: string; channel: ContactChannel; reference: string | null } | null;
  followUps: FollowUpChange[];
  pendingFollowUp: FollowUpChange | null;
  events: ApplicationEvent[];
  submissionApproval?: SubmissionApproval | null;
  submissionAttempt?: SubmissionAttempt | null;
  submissionAttemptResult?: SubmissionAttemptResult | null;
}

export interface ApplicationPage {
  page: number;
  pageSize: number;
  items: ApplicationSummary[];
}

export interface DueFollowUp {
  candidacyId: string;
  jobTitle: string;
  companyName: string;
  status: ApplicationStatus;
  revision: number;
  followUpId: string;
  dueAtUtc: string;
  channel: ContactChannel;
  note: string;
}

export interface DueFollowUpPage {
  page: number;
  pageSize: number;
  asOfUtc: string;
  items: DueFollowUp[];
}

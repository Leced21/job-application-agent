export interface ResumeMetadata {
  id: string;
  fileName: string;
  mediaType: string;
  length: number;
  sha256: string;
  uploadedAtUtc: string;
  status: string;
}

export interface ResumeSeriesSummary {
  id: string;
  name: string;
  createdAtUtc: string;
  versionCount: number;
}

export interface ResumeSeriesVersion {
  seriesId: string;
  versionNumber: number;
  label: string | null;
  addedAtUtc: string;
  resume: ResumeMetadata;
}

export interface ResumeSeriesWriteResult {
  series: ResumeSeriesSummary;
  version: ResumeSeriesVersion;
  created: boolean;
}

export interface PreparedDraftSummary {
  id: string;
  requestId: string;
  resumeId: string;
  jobId: string;
  kind: 'Resume' | 'CoverLetter';
  status: string;
  snapshotSha256: string;
  createdAtUtc: string;
}

export interface DraftFact {
  fact: {
    id: string;
    kind: string;
    unitKind: string;
    decision: string;
    fields: Array<{ field: string; value: string; excerpt: string; segmentIndex: number }>;
  };
  datesOrigin: string;
}

export interface PreparedDraft {
  draft: PreparedDraftSummary;
  snapshot: {
    schemaVersion: string;
    job: { id: string; title: string; companyName: string; location: string | null; description: string | null; source: string; updatedAtUtc: string };
    candidate: { resume: ResumeMetadata; version: { seriesId: string; versionNumber: number; label: string | null } | null; facts: DraftFact[] };
  };
}

export interface ComposedDocument {
  composition: { draftId: string; kind: string; templateVersion: string; sourceSnapshotSha256: string; contentSha256: string; createdAtUtc: string; decision: string; reviewedAtUtc: string | null; reviewNote: string | null };
  content: { title: string; plainText: string; blocks: Array<{ factId: string; kind: string; text: string; datesOrigin: string }>; notices: string[] };
}

export interface CompositionWriteResult {
  document: ComposedDocument;
  created: boolean;
}

export interface DocumentExportMetadata {
  id: string;
  draftId: string;
  format: 'Pdf' | 'Docx';
  rendererVersion: string;
  compositionSha256: string;
  fileSha256: string;
  fileName: string;
  mediaType: string;
  length: number;
  pageCount: number | null;
  createdAtUtc: string;
  approvedAtUtc: string;
}

export interface DocumentExportWriteResult {
  export: DocumentExportMetadata;
  created: boolean;
}

export interface CandidateProfile {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  jobTitle: string | null;
  summary: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
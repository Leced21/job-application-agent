export interface UpdateCandidateProfile {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  jobTitle: string | null;
  summary: string | null;
}
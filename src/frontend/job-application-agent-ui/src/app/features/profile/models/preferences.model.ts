import { UpdatePreferences } from './update-preferences.model';
export interface CandidatePreferences extends UpdatePreferences {
  candidateProfileId: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

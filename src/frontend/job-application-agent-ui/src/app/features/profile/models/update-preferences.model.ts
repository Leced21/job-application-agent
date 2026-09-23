export type WorkMode = 'OnSite' | 'Hybrid' | 'Remote';
export interface UpdatePreferences {
  desiredJobTitles: string[];
  preferredLocations: string[];
  contractTypes: string[];
  workModes: WorkMode[];
  minimumAnnualGrossSalary: number | null;
  salaryCurrency: string | null;
  availableFrom: string | null;
}

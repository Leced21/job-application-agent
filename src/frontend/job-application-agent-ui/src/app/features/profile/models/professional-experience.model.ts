export interface ProfessionalExperience {
  id: string;
  companyName: string;
  jobTitle: string;
  location: string | null;
  startDate: string;
  endDate: string | null;
  isCurrent: boolean;
  description: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
export interface AddProfessionalExperience {
  companyName: string;
  jobTitle: string;
  location: string | null;
  startDate: string;
  endDate: string | null;
  isCurrent: boolean;
  description: string | null;
}
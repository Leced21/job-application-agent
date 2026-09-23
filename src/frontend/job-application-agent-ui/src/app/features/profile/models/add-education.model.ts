export interface AddEducation {
  institutionName: string;
  degree: string;
  fieldOfStudy: string | null;
  location: string | null;
  startDate: string;
  endDate: string | null;
  isCurrent: boolean;
  description: string | null;
}

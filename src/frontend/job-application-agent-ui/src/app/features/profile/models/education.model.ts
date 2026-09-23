import { AddEducation } from './add-education.model';
export interface Education extends AddEducation {
  id: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

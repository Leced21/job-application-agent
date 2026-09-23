import { AddLanguage } from './add-language.model';
export interface Language extends AddLanguage {
  id: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

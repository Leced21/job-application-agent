import { AddLink } from './add-link.model';
export interface Link extends AddLink {
  id: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

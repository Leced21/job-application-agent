import { AddCertification } from './add-certification.model';

export interface Certification extends AddCertification {
  id: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

import { ContractType } from './contract-type.model';
import { JobStatus } from './job-status.model';
import { WorkMode } from './work-mode.model';

export interface Job {
  id: string;
  title: string;
  companyName: string;
  location: string | null;
  workMode: WorkMode;
  contractType: ContractType;
  salaryMin: number | null;
  salaryMax: number | null;
  salaryCurrency: string | null;
  description: string | null;
  source: string;
  sourceUrl: string | null;
  status: JobStatus;
  publishedAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}
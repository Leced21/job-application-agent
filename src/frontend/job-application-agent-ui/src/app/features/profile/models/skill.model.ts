import { AddSkill } from './add-skill.model';
export interface Skill extends AddSkill {
  id: string;
  name: string;
  category: string | null;
  level: string | null;
  yearsOfExperience: number | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

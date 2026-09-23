import { CandidatePreferences } from '../models/preferences.model';
import { UpdatePreferences } from '../models/update-preferences.model';
import { Link } from '../models/link.model';
import { AddLink } from '../models/add-link.model';
import { UpdateLink } from '../models/update-link.model';
import { Language } from '../models/language.model';
import { AddLanguage } from '../models/add-language.model';
import { UpdateLanguage } from '../models/update-language.model';
import { Skill } from '../models/skill.model';
import { AddSkill } from '../models/add-skill.model';
import { UpdateSkill } from '../models/update-skill.model';
import { Education } from '../models/education.model';
import { AddEducation } from '../models/add-education.model';
import { UpdateEducation } from '../models/update-education.model';
import { Certification } from '../models/certification.model';
import { AddCertification } from '../models/add-certification.model';
import { UpdateCertification } from '../models/update-certification.model';
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { CandidateProfile } from '../models/candidate-profile.model';
import { UpdateCandidateProfile } from '../models/update-candidate-profile.model';
import { UpdateProfessionalExperience } from '../models/update-professional-experience.model';
import { ProfessionalExperience } from '../models/professional-experience.model';
import { AddProfessionalExperience } from '../models/add-professional-experience.model';

@Injectable({
    providedIn: 'root'
})
export class ProfileApiService {
    private readonly http = inject(HttpClient);

    private readonly baseUrl = '/api/v1/profile';

    getProfile(): Observable<CandidateProfile> {
        return this.http.get<CandidateProfile>(this.baseUrl);
    }
    updateProfile(command: UpdateCandidateProfile): Observable<CandidateProfile> {
        return this.http.put<CandidateProfile>(this.baseUrl, command);
    }
    getExperiences(): Observable<ProfessionalExperience[]> {
        return this.http.get<ProfessionalExperience[]>(
            `${this.baseUrl}/experiences`
        );
    }

    addExperience(
        command: AddProfessionalExperience
    ): Observable<ProfessionalExperience> {
        return this.http.post<ProfessionalExperience>(
            `${this.baseUrl}/experiences`,
            command
        );
    }

    updateExperience(
        experienceId: string,
        command: UpdateProfessionalExperience
    ): Observable<ProfessionalExperience> {
        return this.http.put<ProfessionalExperience>(
            `${this.baseUrl}/experiences/${experienceId}`,
            command
        );
    }

    deleteExperience(
        experienceId: string
    ): Observable<void> {
        return this.http.delete<void>(
            `${this.baseUrl}/experiences/${experienceId}`
        );
    }

    getCertifications(): Observable<Certification[]> {
        return this.http.get<Certification[]>(`${this.baseUrl}/certifications`);
    }

    addCertification(command: AddCertification): Observable<Certification> {
        return this.http.post<Certification>(`${this.baseUrl}/certifications`, command);
    }

    updateCertification(id: string, command: UpdateCertification): Observable<Certification> {
        return this.http.put<Certification>(`${this.baseUrl}/certifications/${id}`, command);
    }

    deleteCertification(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/certifications/${id}`);
    }
    getEducations(): Observable<Education[]> {
        return this.http.get<Education[]>(`${this.baseUrl}/educations`);
    }

    addEducation(command: AddEducation): Observable<Education> {
        return this.http.post<Education>(`${this.baseUrl}/educations`, command);
    }

    updateEducation(id: string, command: UpdateEducation): Observable<Education> {
        return this.http.put<Education>(`${this.baseUrl}/educations/${id}`, command);
    }

    deleteEducation(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/educations/${id}`);
    }
    getSkills(): Observable<Skill[]> {
        return this.http.get<Skill[]>(`${this.baseUrl}/skills`);
    }

    addSkill(command: AddSkill): Observable<Skill> {
        return this.http.post<Skill>(`${this.baseUrl}/skills`, command);
    }

    updateSkill(id: string, command: UpdateSkill): Observable<Skill> {
        return this.http.put<Skill>(`${this.baseUrl}/skills/${id}`, command);
    }

    deleteSkill(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/skills/${id}`);
    }
    getLanguages(): Observable<Language[]> {
        return this.http.get<Language[]>(`${this.baseUrl}/languages`);
    }

    addLanguage(command: AddLanguage): Observable<Language> {
        return this.http.post<Language>(`${this.baseUrl}/languages`, command);
    }

    updateLanguage(id: string, command: UpdateLanguage): Observable<Language> {
        return this.http.put<Language>(`${this.baseUrl}/languages/${id}`, command);
    }

    deleteLanguage(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/languages/${id}`);
    }
    getLinks(): Observable<Link[]> {
        return this.http.get<Link[]>(`${this.baseUrl}/links`);
    }

    addLink(command: AddLink): Observable<Link> {
        return this.http.post<Link>(`${this.baseUrl}/links`, command);
    }

    updateLink(id: string, command: UpdateLink): Observable<Link> {
        return this.http.put<Link>(`${this.baseUrl}/links/${id}`, command);
    }

    deleteLink(id: string): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/links/${id}`);
    }

    getPreferences(): Observable<CandidatePreferences> {
        return this.http.get<CandidatePreferences>(`${this.baseUrl}/preferences`);
    }
    addPreferences(command: UpdatePreferences): Observable<CandidatePreferences> {
        return this.http.post<CandidatePreferences>(`${this.baseUrl}/preferences`, command);
    }
    updatePreferences(command: UpdatePreferences): Observable<CandidatePreferences> {
        return this.http.put<CandidatePreferences>(`${this.baseUrl}/preferences`, command);
    }
    deletePreferences(): Observable<void> {
        return this.http.delete<void>(`${this.baseUrl}/preferences`);
    }
}
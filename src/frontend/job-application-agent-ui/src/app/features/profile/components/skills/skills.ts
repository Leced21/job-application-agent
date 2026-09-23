import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Component, DestroyRef, inject, signal } from '@angular/core';

import { ProfileApiService } from '../../data-access/profile-api.service';
import { Skill } from '../../models/skill.model';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AddSkill } from '../../models/add-skill.model';

@Component({
  selector: 'app-skills',
  imports: [ReactiveFormsModule],
  templateUrl: './skills.html',
  styleUrl: './skills.scss'
})
export class Skills {
  private readonly destroyRef = inject(DestroyRef);
  private readonly profileApi = inject(ProfileApiService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly editingId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly confirmingDeleteId = signal<string | null>(null);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly adding = signal(false);
  protected readonly saving = signal(false)

  protected readonly skills = signal<Skill[]>([]);
  protected readonly loading = signal(true);
  protected readonly saveError = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(150)]],
    category: ['', Validators.maxLength(100)],
    level: ['', Validators.maxLength(50)],
    yearsOfExperience: [null as number | null, [Validators.min(0), Validators.max(2147483647), Validators.pattern(/^\d+$/)]]
  });

  constructor() {
    this.loadSkills();
  }

  protected get categories(): string[] {
    const categories = this.skills()
      .map((skill) => skill.category?.trim())
      .filter((category): category is string => Boolean(category));

    return [...new Set(categories)].sort((a, b) =>
      a.localeCompare(b, 'fr', { sensitivity: 'base' })
    );
  }

  protected skillsForCategory(category: string): Skill[] {
    return this.skills().filter(
      (skill) => skill.category?.trim() === category
    );
  }

  protected get uncategorizedSkills(): Skill[] {
    return this.skills().filter(
      (skill) => !skill.category?.trim()
    );
  }

  protected experienceLabel(years: number | null): string | null {
    if (years === null) {
      return null;
    }

    if (years === 0) {
      return '0 année';
    }

    return years === 1 ? '1 an' : `${years} ans`;
  }
  protected startAdding(): void {
    if (this.saving() || this.deletingId()) return;
    this.editingId.set(null);
    this.cancelDelete();
    this.form.reset({
      name: '',
      category: '',
      level: '',
      yearsOfExperience: null
    });

    this.saveError.set(null);
    this.adding.set(true);
  }

  protected startEditing(skill: Skill): void {
    if (this.saving() || this.deletingId()) return;
    this.adding.set(false);
    this.editingId.set(skill.id);
    this.cancelDelete();
    this.saveError.set(null);
    this.form.reset({ name: skill.name, category: skill.category ?? '', level: skill.level ?? '', yearsOfExperience: skill.yearsOfExperience });
  }

  protected cancelForm(): void {
    if (this.saving()) return;
    this.editingId.set(null);
    this.cancelAdding();
  }

  protected cancelAdding(): void {
    if (this.saving()) return;
    this.adding.set(false);
    this.saveError.set(null);
  }

  protected saveSkill(): void {
    if (this.saving() || this.deletingId()) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();

    const command: AddSkill = {
      name: value.name.trim(),
      category: this.toNullable(value.category),
      level: this.toNullable(value.level),
      yearsOfExperience: value.yearsOfExperience
    };

    this.saving.set(true);
    this.saveError.set(null);

    const id = this.editingId();
    const request = id ? this.profileApi.updateSkill(id, command) : this.profileApi.addSkill(command);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (skill) => {
        this.skills.update((skills) =>
          [...skills.filter(existing => existing.id !== skill.id), skill].sort((a, b) =>
            a.name.localeCompare(
              b.name,
              'fr',
              { sensitivity: 'base' }
            )
          )
        );

        this.editingId.set(null);
        this.adding.set(false);
        this.saving.set(false);
      },

      error: (error) => {
        console.error('Unable to add skill.', error);

        this.saveError.set(
          'Impossible d’enregistrer cette compétence. Vos saisies sont conservées.'
        );

        this.saving.set(false);
      }
    });
  }

  protected requestDelete(id: string): void {
    if (this.saving() || this.deletingId()) return;
    this.confirmingDeleteId.set(id);
    this.deleteError.set(null);
  }

  protected cancelDelete(): void {
    if (this.deletingId()) return;
    this.confirmingDeleteId.set(null);
    this.deleteError.set(null);
  }

  protected deleteSkill(id: string): void {
    if (this.confirmingDeleteId() !== id || this.deletingId() || this.saving()) return;
    this.deletingId.set(id);
    this.deleteError.set(null);
    this.profileApi.deleteSkill(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.skills.update(items => items.filter(item => item.id !== id));
        this.deletingId.set(null);
        this.cancelDelete();
        if (this.editingId() === id) this.cancelForm();
      },
      error: () => {
        this.deletingId.set(null);
        this.deleteError.set('Impossible de supprimer cette compétence. Réessayez.');
      }
    });
  }

  private toNullable(value: string): string | null {
    const trimmedValue = value.trim();

    return trimmedValue.length === 0
      ? null
      : trimmedValue;
  }

  protected loadSkills(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.profileApi.getSkills().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (skills) => {
        this.skills.set([...skills].sort((a, b) => a.name.localeCompare(b.name, 'fr', { sensitivity: 'base' })));
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Unable to load skills.', error);
        this.loadError.set('Impossible de charger les compétences.');
        this.loading.set(false);
      }
    });
  }
}
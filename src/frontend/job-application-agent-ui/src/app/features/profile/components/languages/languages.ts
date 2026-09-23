import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { ProfileApiService } from '../../data-access/profile-api.service';
import { Language } from '../../models/language.model';

const requiredText: ValidatorFn = control =>
  typeof control.value === 'string' && control.value.trim() ? null : { required: true };

@Component({
  selector: 'app-languages',
  imports: [ReactiveFormsModule],
  templateUrl: './languages.html',
  styleUrl: './languages.scss'
})
export class Languages {
  private readonly api = inject(ProfileApiService);
  private readonly builder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly languages = signal<Language[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly adding = signal(false);
  protected readonly editingId = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

  protected readonly form = this.builder.nonNullable.group({
    name: ['', [requiredText, Validators.maxLength(100)]],
    proficiencyLevel: ['', [requiredText, Validators.maxLength(50)]]
  });

  constructor() { this.loadLanguages(); }

  protected loadLanguages(): void {
    if (this.saving() || this.deleting()) return;
    this.loading.set(true);
    this.error.set(null);
    this.api.getLanguages().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: items => { this.setSorted(items); this.loading.set(false); },
      error: () => {
        this.error.set('Impossible de charger les langues.');
        this.loading.set(false);
      }
    });
  }

  protected startAdding(): void {
    if (this.saving() || this.deleting()) return;
    this.form.reset();
    this.editingId.set(null);
    this.adding.set(true);
    this.saveError.set(null);
    this.cancelDelete();
  }

  protected startEditing(item: Language): void {
    if (this.saving() || this.deleting()) return;
    this.form.reset({ name: item.name, proficiencyLevel: item.proficiencyLevel });
    this.adding.set(false);
    this.editingId.set(item.id);
    this.saveError.set(null);
    this.cancelDelete();
  }

  protected cancelForm(): void {
    if (this.saving()) return;
    this.adding.set(false);
    this.editingId.set(null);
    this.saveError.set(null);
  }

  protected saveLanguage(): void {
    if (this.saving() || this.deleting()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const command = { name: value.name.trim(), proficiencyLevel: value.proficiencyLevel.trim() };
    const id = this.editingId();
    this.saving.set(true);
    this.saveError.set(null);
    const request = id ? this.api.updateLanguage(id, command) : this.api.addLanguage(command);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => {
        this.setSorted(id ? this.languages().map(item => item.id === id ? saved : item)
          : [...this.languages(), saved]);
        this.saving.set(false);
        this.cancelForm();
      },
      error: () => {
        this.saveError.set('Impossible d’enregistrer la langue. Vos informations sont conservées.');
        this.saving.set(false);
      }
    });
  }

  protected requestDelete(id: string): void {
    if (this.saving() || this.deleting()) return;
    this.deletingId.set(id);
    this.deleteError.set(null);
  }

  protected cancelDelete(): void {
    if (this.deleting()) return;
    this.deletingId.set(null);
    this.deleteError.set(null);
  }

  protected deleteLanguage(id: string): void {
    if (this.deleting() || this.saving() || this.deletingId() !== id) return;
    this.deleting.set(true);
    this.deleteError.set(null);
    this.api.deleteLanguage(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.languages.update(items => items.filter(item => item.id !== id));
        if (this.editingId() === id) this.cancelForm();
        this.deleting.set(false);
        this.cancelDelete();
      },
      error: () => {
        this.deleting.set(false);
        this.deleteError.set('Impossible de supprimer cette langue. Réessayez.');
      }
    });
  }

  private setSorted(items: Language[]): void {
    this.languages.set([...items].sort((a, b) => a.name.localeCompare(b.name, 'fr')));
  }

}

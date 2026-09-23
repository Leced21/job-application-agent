import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { ProfileApiService } from '../../data-access/profile-api.service';
import { Education } from '../../models/education.model';

const requiredText: ValidatorFn = control =>
  typeof control.value === 'string' && control.value.trim() ? null : { required: true };

@Component({
  selector: 'app-educations',
  imports: [ReactiveFormsModule],
  templateUrl: './educations.html',
  styleUrl: './educations.scss'
})
export class Educations {
  private readonly api = inject(ProfileApiService);
  private readonly builder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly educations = signal<Education[]>([]);
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
    institutionName: ['', [requiredText, Validators.maxLength(200)]],
    degree: ['', [requiredText, Validators.maxLength(150)]],
    fieldOfStudy: ['', Validators.maxLength(200)],
    location: ['', Validators.maxLength(200)],
    startDate: ['', Validators.required],
    endDate: [''],
    isCurrent: [false],
    description: ['', Validators.maxLength(4000)]
  }, { validators: control => {
    const { startDate, endDate, isCurrent } = control.getRawValue();
    return !isCurrent && startDate && endDate && endDate < startDate ? { dateOrder: true } : null;
  } });

  constructor() {
    this.loadEducations();
    this.form.controls.isCurrent.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(current => {
      if (current) {
        this.form.controls.endDate.setValue('');
        this.form.controls.endDate.disable();
      } else {
        this.form.controls.endDate.enable();
      }
    });
  }

  protected loadEducations(): void {
    if (this.saving() || this.deleting()) return;
    this.loading.set(true);
    this.error.set(null);
    this.api.getEducations().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: items => { this.setSorted(items); this.loading.set(false); },
      error: () => {
        this.error.set('Impossible de charger les formations.');
        this.loading.set(false);
      }
    });
  }

  protected startAdding(): void {
    if (this.saving() || this.deleting()) return;
    this.form.reset();
    this.form.controls.endDate.enable();
    this.editingId.set(null);
    this.adding.set(true);
    this.saveError.set(null);
    this.cancelDelete();
  }

  protected startEditing(item: Education): void {
    if (this.saving() || this.deleting()) return;
    this.form.reset({
      institutionName: item.institutionName, degree: item.degree,
      fieldOfStudy: item.fieldOfStudy ?? '', location: item.location ?? '',
      startDate: item.startDate, endDate: item.endDate ?? '',
      isCurrent: item.isCurrent, description: item.description ?? ''
    });
    if (item.isCurrent) this.form.controls.endDate.disable();
    else this.form.controls.endDate.enable();
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

  protected saveEducation(): void {
    if (this.saving() || this.deleting()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const command = {
      institutionName: value.institutionName.trim(), degree: value.degree.trim(),
      fieldOfStudy: this.toNullable(value.fieldOfStudy), location: this.toNullable(value.location),
      startDate: value.startDate, endDate: value.isCurrent ? null : this.toNullable(value.endDate),
      isCurrent: value.isCurrent, description: this.toNullable(value.description)
    };
    const id = this.editingId();
    this.saving.set(true);
    this.saveError.set(null);
    const request = id ? this.api.updateEducation(id, command) : this.api.addEducation(command);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => {
        this.setSorted(id ? this.educations().map(item => item.id === id ? saved : item)
          : [...this.educations(), saved]);
        this.saving.set(false);
        this.cancelForm();
      },
      error: () => {
        this.saveError.set('Impossible d’enregistrer la formation. Vos informations sont conservées.');
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

  protected deleteEducation(id: string): void {
    if (this.deleting() || this.saving() || this.deletingId() !== id) return;
    this.deleting.set(true);
    this.deleteError.set(null);
    this.api.deleteEducation(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.educations.update(items => items.filter(item => item.id !== id));
        if (this.editingId() === id) this.cancelForm();
        this.deleting.set(false);
        this.cancelDelete();
      },
      error: () => {
        this.deleting.set(false);
        this.deleteError.set('Impossible de supprimer cette formation. Réessayez.');
      }
    });
  }

  protected formatDate(value: string): string {
    const [year, month, day] = value.split('-').map(Number);
    return new Intl.DateTimeFormat('fr-FR').format(new Date(year, month - 1, day));
  }

  private setSorted(items: Education[]): void {
    this.educations.set([...items].sort((a, b) => b.startDate.localeCompare(a.startDate)));
  }

  private toNullable(value: string): string | null { return value.trim() || null; }
}

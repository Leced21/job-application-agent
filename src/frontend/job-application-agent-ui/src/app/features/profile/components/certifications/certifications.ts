import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { ProfileApiService } from '../../data-access/profile-api.service';
import { Certification } from '../../models/certification.model';

const requiredText: ValidatorFn = control =>
  typeof control.value === 'string' && control.value.trim() ? null : { required: true };

const httpUrl: ValidatorFn = control => {
  const value = (control.value as string).trim();
  if (!value) return null;
  try {
    return ['http:', 'https:'].includes(new URL(value).protocol) ? null : { url: true };
  } catch {
    return { url: true };
  }
};

@Component({
  selector: 'app-certifications',
  imports: [ReactiveFormsModule],
  templateUrl: './certifications.html',
  styleUrl: './certifications.scss'
})
export class Certifications {
  private readonly api = inject(ProfileApiService);
  private readonly builder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly certifications = signal<Certification[]>([]);
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
    name: ['', [requiredText, Validators.maxLength(200)]],
    issuingOrganization: ['', [requiredText, Validators.maxLength(200)]],
    issueDate: ['', Validators.required],
    expirationDate: [''],
    credentialId: ['', Validators.maxLength(200)],
    credentialUrl: ['', [Validators.maxLength(2000), httpUrl]]
  }, { validators: control => {
    const { issueDate, expirationDate } = control.getRawValue();
    return issueDate && expirationDate && expirationDate < issueDate
      ? { dateOrder: true } : null;
  } });

  constructor() { this.loadCertifications(); }

  protected loadCertifications(): void {
    if (this.saving() || this.deleting()) return;
    this.loading.set(true);
    this.error.set(null);
    this.api.getCertifications().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: items => { this.setSorted(items); this.loading.set(false); },
      error: () => {
        this.error.set('Impossible de charger les certifications.');
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

  protected startEditing(item: Certification): void {
    if (this.saving() || this.deleting()) return;
    this.form.reset({
      name: item.name, issuingOrganization: item.issuingOrganization,
      issueDate: item.issueDate, expirationDate: item.expirationDate ?? '',
      credentialId: item.credentialId ?? '', credentialUrl: item.credentialUrl ?? ''
    });
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

  protected saveCertification(): void {
    if (this.saving() || this.deleting()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const command = {
      name: value.name.trim(), issuingOrganization: value.issuingOrganization.trim(),
      issueDate: value.issueDate, expirationDate: this.toNullable(value.expirationDate),
      credentialId: this.toNullable(value.credentialId), credentialUrl: this.toNullable(value.credentialUrl)
    };
    const id = this.editingId();
    this.saving.set(true);
    this.saveError.set(null);
    const request = id ? this.api.updateCertification(id, command) : this.api.addCertification(command);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => {
        this.setSorted(id ? this.certifications().map(item => item.id === id ? saved : item)
          : [...this.certifications(), saved]);
        this.saving.set(false);
        this.cancelForm();
      },
      error: () => {
        this.saveError.set('Impossible d’enregistrer la certification. Vos informations sont conservées.');
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

  protected deleteCertification(id: string): void {
    if (this.deleting() || this.saving() || this.deletingId() !== id) return;
    this.deleting.set(true);
    this.deleteError.set(null);
    this.api.deleteCertification(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.certifications.update(items => items.filter(item => item.id !== id));
        if (this.editingId() === id) this.cancelForm();
        this.deleting.set(false);
        this.cancelDelete();
      },
      error: () => {
        this.deleting.set(false);
        this.deleteError.set('Impossible de supprimer cette certification. Réessayez.');
      }
    });
  }

  protected safeUrl(value: string | null): string | null {
    if (!value) return null;
    try { return ['http:', 'https:'].includes(new URL(value).protocol) ? value : null; }
    catch { return null; }
  }

  protected formatDate(value: string): string {
    const [year, month, day] = value.split('-').map(Number);
    return new Intl.DateTimeFormat('fr-FR').format(new Date(year, month - 1, day));
  }

  private setSorted(items: Certification[]): void {
    this.certifications.set([...items].sort((a, b) => a.name.localeCompare(b.name, 'fr')));
  }

  private toNullable(value: string): string | null { return value.trim() || null; }
}

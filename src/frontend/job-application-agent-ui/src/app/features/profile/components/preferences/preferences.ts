import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, ValidatorFn } from '@angular/forms';
import { ProfileApiService } from '../../data-access/profile-api.service';
import { CandidatePreferences } from '../../models/preferences.model';
import { UpdatePreferences, WorkMode } from '../../models/update-preferences.model';

const lines = (value: string): string[] => value.split(/\r?\n/).map(item => item.trim()).filter(Boolean);
const listValidator = (maxLength: number): ValidatorFn => control => {
  const items = lines(control.value);
  return items.length <= 20 && items.every(item => item.length <= maxLength) ? null : { list: true };
};
const salaryValidator: ValidatorFn = control => {
  const value = control.value;
  return value === null || (Number.isFinite(value) && value >= 0 && value <= 9999999999.99 &&
    /^\d+(\.\d{1,2})?$/.test(String(value))) ? null : { salary: true };
};

@Component({
  selector: 'app-preferences',
  imports: [ReactiveFormsModule],
  templateUrl: './preferences.html',
  styleUrl: './preferences.scss'
})
export class Preferences {
  private readonly api = inject(ProfileApiService);
  private readonly builder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly preferences = signal<CandidatePreferences | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly editing = signal(false);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly confirmingDelete = signal(false);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);
  protected readonly workModes: { value: WorkMode; label: string }[] = [
    { value: 'OnSite', label: 'Sur site' }, { value: 'Hybrid', label: 'Hybride' },
    { value: 'Remote', label: 'Télétravail' }
  ];
  protected readonly form = this.builder.group({
    desiredJobTitles: this.builder.nonNullable.control('', listValidator(150)),
    preferredLocations: this.builder.nonNullable.control('', listValidator(200)),
    contractTypes: this.builder.nonNullable.control('', listValidator(100)),
    onSite: this.builder.nonNullable.control(false),
    hybrid: this.builder.nonNullable.control(false),
    remote: this.builder.nonNullable.control(false),
    minimumAnnualGrossSalary: this.builder.control<number | null>(null, salaryValidator),
    salaryCurrency: this.builder.nonNullable.control(''),
    availableFrom: this.builder.nonNullable.control('')
  }, { validators: control => {
    const { minimumAnnualGrossSalary, salaryCurrency } = control.getRawValue();
    return minimumAnnualGrossSalary !== null && !/^[A-Z]{3}$/.test(salaryCurrency.trim().toUpperCase())
      ? { currency: true } : null;
  } });

  constructor() { this.loadPreferences(); }

  protected loadPreferences(): void {
    this.loading.set(true); this.error.set(null);
    this.api.getPreferences().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { this.preferences.set(value); this.loading.set(false); },
      error: error => {
        if (error.status === 404) this.preferences.set(null);
        else this.error.set('Impossible de charger les préférences.');
        this.loading.set(false);
      }
    });
  }
  protected startEditing(): void {
    if (this.saving() || this.deleting()) return;
    const value = this.preferences();
    this.form.reset({
      desiredJobTitles: value?.desiredJobTitles.join('\n') ?? '',
      preferredLocations: value?.preferredLocations.join('\n') ?? '',
      contractTypes: value?.contractTypes.join('\n') ?? '',
      onSite: value?.workModes.includes('OnSite') ?? false,
      hybrid: value?.workModes.includes('Hybrid') ?? false,
      remote: value?.workModes.includes('Remote') ?? false,
      minimumAnnualGrossSalary: value?.minimumAnnualGrossSalary ?? null,
      salaryCurrency: value?.salaryCurrency ?? '',
      availableFrom: value?.availableFrom ?? ''
    });
    this.saveError.set(null); this.editing.set(true); this.cancelDelete();
  }
  protected cancelForm(): void {
    if (this.saving()) return;
    this.editing.set(false); this.saveError.set(null);
  }
  protected savePreferences(): void {
    if (this.saving() || this.deleting()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const v = this.form.getRawValue();
    const workModes: WorkMode[] = [];
    if (v.onSite) workModes.push('OnSite');
    if (v.hybrid) workModes.push('Hybrid');
    if (v.remote) workModes.push('Remote');
    const command: UpdatePreferences = {
      desiredJobTitles: lines(v.desiredJobTitles), preferredLocations: lines(v.preferredLocations),
      contractTypes: lines(v.contractTypes), workModes,
      minimumAnnualGrossSalary: v.minimumAnnualGrossSalary,
      salaryCurrency: v.minimumAnnualGrossSalary === null ? null : v.salaryCurrency.trim().toUpperCase(),
      availableFrom: v.availableFrom || null
    };
    this.saving.set(true); this.saveError.set(null);
    const request = this.preferences() ? this.api.updatePreferences(command) : this.api.addPreferences(command);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { this.preferences.set(value); this.saving.set(false); this.cancelForm(); },
      error: () => { this.saving.set(false); this.saveError.set('Impossible d’enregistrer les préférences. Vos saisies sont conservées.'); }
    });
  }
  protected requestDelete(): void {
    if (this.saving() || this.deleting()) return;
    this.confirmingDelete.set(true); this.deleteError.set(null);
  }
  protected cancelDelete(): void {
    if (this.deleting()) return;
    this.confirmingDelete.set(false); this.deleteError.set(null);
  }
  protected deletePreferences(): void {
    if (!this.confirmingDelete() || this.deleting() || this.saving()) return;
    this.deleting.set(true); this.deleteError.set(null);
    this.api.deletePreferences().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.preferences.set(null); this.deleting.set(false); this.cancelDelete(); this.cancelForm();
      },
      error: () => { this.deleting.set(false); this.deleteError.set('Impossible de supprimer les préférences. Réessayez.'); }
    });
  }
  protected modesLabel(modes: WorkMode[]): string {
    return this.workModes.filter(mode => modes.includes(mode.value)).map(mode => mode.label).join(', ') || 'Sans préférence';
  }
}

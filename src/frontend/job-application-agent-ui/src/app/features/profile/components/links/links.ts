import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { ProfileApiService } from '../../data-access/profile-api.service';
import { Link } from '../../models/link.model';

const requiredText: ValidatorFn = control =>
  typeof control.value === 'string' && control.value.trim() ? null : { required: true };

const httpUrl: ValidatorFn = control => {
  try {
    const value = (control.value as string).trim();
    return /^https?:\/\//i.test(value) && ['http:', 'https:'].includes(new URL(value).protocol)
      ? null : { url: true };
  } catch { return { url: true }; }
};

@Component({
  selector: 'app-links',
  imports: [ReactiveFormsModule],
  templateUrl: './links.html',
  styleUrl: './links.scss'
})
export class Links {
  private readonly api = inject(ProfileApiService);
  private readonly builder = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly links = signal<Link[]>([]);
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
    url: ['', [requiredText, Validators.maxLength(2000), httpUrl]]
  });

  constructor() { this.loadLinks(); }

  protected loadLinks(): void {
    if (this.saving() || this.deleting()) return;
    this.loading.set(true);
    this.error.set(null);
    this.api.getLinks().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: items => { this.setSorted(items); this.loading.set(false); },
      error: () => {
        this.error.set('Impossible de charger les liens.');
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

  protected startEditing(item: Link): void {
    if (this.saving() || this.deleting()) return;
    this.form.reset({ name: item.name, url: item.url });
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

  protected saveLink(): void {
    if (this.saving() || this.deleting()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const command = { name: value.name.trim(), url: value.url.trim() };
    const id = this.editingId();
    this.saving.set(true);
    this.saveError.set(null);
    const request = id ? this.api.updateLink(id, command) : this.api.addLink(command);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => {
        this.setSorted(id ? this.links().map(item => item.id === id ? saved : item)
          : [...this.links(), saved]);
        this.saving.set(false);
        this.cancelForm();
      },
      error: () => {
        this.saveError.set('Impossible d’enregistrer le lien. Vos informations sont conservées.');
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

  protected deleteLink(id: string): void {
    if (this.deleting() || this.saving() || this.deletingId() !== id) return;
    this.deleting.set(true);
    this.deleteError.set(null);
    this.api.deleteLink(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.links.update(items => items.filter(item => item.id !== id));
        if (this.editingId() === id) this.cancelForm();
        this.deleting.set(false);
        this.cancelDelete();
      },
      error: () => {
        this.deleting.set(false);
        this.deleteError.set('Impossible de supprimer ce lien. Réessayez.');
      }
    });
  }

  protected safeUrl(value: string): string | null {
    try {
      return /^https?:\/\//i.test(value) && ['http:', 'https:'].includes(new URL(value).protocol)
        ? value : null;
    } catch { return null; }
  }

  private setSorted(items: Link[]): void {
    this.links.set([...items].sort((a, b) => a.name.localeCompare(b.name, 'fr')));
  }

}

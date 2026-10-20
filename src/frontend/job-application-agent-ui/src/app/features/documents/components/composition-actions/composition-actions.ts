import { Component, input, output } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ComposedDocument, DocumentExportMetadata } from '../../models/document.models';

@Component({
  selector: 'app-composition-actions',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './composition-actions.html',
  styleUrl: './composition-actions.scss'
})
export class CompositionActions {
  readonly composition = input<ComposedDocument | null>(null);
  readonly exports = input<DocumentExportMetadata[]>([]);
  readonly busy = input(false);
  readonly reviewConfirmed = input(false);
  readonly reviewNote = input('');
  readonly composeRequested = output<void>();
  readonly reviewConfirmedChange = output<boolean>();
  readonly reviewNoteChange = output<string>();
  readonly reviewRequested = output<'Approved' | 'Rejected'>();
  readonly exportRequested = output<'Pdf' | 'Docx'>();
  readonly downloadRequested = output<DocumentExportMetadata>();
}

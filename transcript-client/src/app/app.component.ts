import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { saveAs } from 'file-saver';
import jsPDF from 'jspdf';
import { TranscriptApiService } from './core/services/transcript-api.service';
import { TranscriptRequest, TranscriptResponse } from './core/models/transcript.model';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AppComponent {
  private readonly fb = inject(FormBuilder);
  private readonly transcriptApi = inject(TranscriptApiService);

  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly transcript = signal<TranscriptResponse | null>(null);
  protected readonly samples = [
    'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
    'https://www.youtube.com/watch?v=QH2-TGUlwu4',
    'https://www.youtube.com/watch?v=jNQXAC9IVRw'
  ];

  readonly form = this.fb.nonNullable.group({
    url: ['', [Validators.required]],
    language: ['']
  });

  protected readonly trackSummary = computed(() => {
    const result = this.transcript();
    if (!result) {
      return '';
    }

    const typeLabel = result.trackType === 'auto' ? 'Auto-generated captions' : 'Manual captions';
    return `${typeLabel} • ${result.sourceLanguage.toUpperCase()}`;
  });

  protected get controls() {
    return this.form.controls;
  }

  protected readonly trackByParagraph = (_: number, paragraph: string) => paragraph;

  handleSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload: TranscriptRequest = {
      url: this.controls.url.value.trim(),
      language: this.controls.language.value?.trim() || undefined
    };

    this.loading.set(true);
    this.errorMessage.set(null);
    this.transcript.set(null);

    this.transcriptApi
      .fetchTranscript(payload)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => this.transcript.set(response),
        error: (error) => {
          const message = (error?.error?.error as string) ?? 'Unable to fetch the transcript right now.';
          this.errorMessage.set(message);
        }
      });
  }

  download(format: 'txt' | 'pdf'): void {
    const transcript = this.transcript();
    if (!transcript) {
      return;
    }

    if (format === 'txt') {
      const blob = new Blob([transcript.fullText], { type: 'text/plain;charset=utf-8' });
      saveAs(blob, `${transcript.videoId}.txt`);
      return;
    }

    const doc = new jsPDF({ unit: 'pt', format: 'a4' });
    const margin = 40;
    const usableWidth = doc.internal.pageSize.getWidth() - margin * 2;
    const lines = doc.splitTextToSize(transcript.fullText, usableWidth);

    let cursorY = margin;
    const lineHeight = 16;

    lines.forEach((line: string) => {
      if (cursorY > doc.internal.pageSize.getHeight() - margin) {
        doc.addPage();
        cursorY = margin;
      }

      doc.text(line, margin, cursorY);
      cursorY += lineHeight;
    });

    doc.save(`${transcript.videoId}.pdf`);
  }

  protected applySample(url: string): void {
    this.controls.url.setValue(url);
  }
}

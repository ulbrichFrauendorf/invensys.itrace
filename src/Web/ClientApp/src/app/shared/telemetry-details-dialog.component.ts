import { CommonModule } from '@angular/common';
import { Component, ChangeDetectionStrategy } from '@angular/core';
import { IDynamicDialogRef, IButton, ICard } from 'invensys-ng';

export interface TelemetryDetailRow {
  label: string;
  value: string;
}

export interface TelemetryDetailsDialogData {
  details: TelemetryDetailRow[];
  message?: string;
  stackTrace?: string;
  attributes?: TelemetryDetailRow[];
  copyAndClose?: boolean;
}

@Component({
  selector: 'app-telemetry-details-dialog',
  imports: [CommonModule, IButton, ICard],
  template: `
    <div class="error-details">
      @if (data.message) {
        <i-card title="Error message">
          <p class="message">{{ data.message }}</p>
        </i-card>
      }
      @if (data.details.length) {
        <i-card title="Event context">
          <dl class="telemetry-details">
            @for (detail of data.details; track detail.label) {
              <div>
                <dt>{{ detail.label }}</dt>
                <dd>{{ detail.value }}</dd>
              </div>
            }
          </dl>
        </i-card>
      }
      @if (data.stackTrace) {
        <i-card title="Stack trace">
          <pre class="stack-trace" tabindex="0" aria-label="Full stack trace">{{
            data.stackTrace
          }}</pre>
        </i-card>
      }
      @if (data.attributes?.length) {
        <i-card title="Additional context">
          <dl class="telemetry-details">
            @for (attribute of data.attributes; track attribute.label) {
              <div>
                <dt>{{ attribute.label }}</dt>
                <dd>{{ attribute.value }}</dd>
              </div>
            }
          </dl>
        </i-card>
      }
      @if (data.copyAndClose) {
        <div class="dialog-actions">
          <i-button icon="pi pi-info-circle" severity="info" (clicked)="copyAndCloseStackTrace()">
            Copy and close
          </i-button>
        </div>
      }
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [
    `
      .error-details {
        display: grid;
        gap: 1rem;
        min-width: 0;
        .dialog-actions {
          display: flex;
          justify-content: flex-end;
          border-top: 1px solid var(--surface-border);
          padding-top: 1rem;
        }
        .message {
          white-space: pre-wrap;
          overflow-wrap: anywhere;
          line-height: 1.6;
        }
        .stack-trace {
          margin: 0;
          padding: 1rem;
          border-radius: 0.5rem;
          background: var(--surface-ground);
          border: 1px solid var(--surface-border);
          overflow: auto;
          max-height: 60vh;
          white-space: pre-wrap;
          overflow-wrap: anywhere;
          font:
            0.85rem/1.7 ui-monospace,
            Consolas,
            monospace;
          tab-size: 4;
        }
        .telemetry-details {
          display: grid;
          grid-template-columns: repeat(auto-fit, minmax(min(100%, 16rem), 1fr));
          gap: 1rem;
          margin: 0;
          div {
            min-width: 0;
            border-bottom: 1px solid var(--surface-border);
            display: grid;
            align-content: start;
            gap: 0.35rem;
            padding-bottom: 0.75rem;
            dt {
              color: var(--color-text-secondary);
              font-size: 0.85rem;
              font-weight: 600;
            }
            dd {
              margin: 0;
              overflow-wrap: anywhere;
              white-space: pre-wrap;
            }
          }
        }
      }
    `,
  ],
})
export class TelemetryDetailsDialogComponent {
  data: TelemetryDetailsDialogData = { details: [] };
  dialogRef?: IDynamicDialogRef;

  protected async copyAndCloseStackTrace(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.data.stackTrace ?? '');
      this.dialogRef?.close();
    } catch {
      const textArea = document.createElement('textarea');
      textArea.value = this.data.stackTrace ?? '';
      textArea.style.position = 'fixed';
      textArea.style.opacity = '0';
      document.body.appendChild(textArea);
      textArea.select();
      const copied = document.execCommand('copy');
      textArea.remove();

      if (copied) {
        this.dialogRef?.close();
      }
    }
  }
}

import { CommonModule } from '@angular/common';
import { Component, ChangeDetectionStrategy, signal } from '@angular/core';
import { IButton, ICard } from 'invensys-ng';

export interface TelemetryDetailRow {
  label: string;
  value: string;
}

export interface TelemetryDetailsDialogData {
  details: TelemetryDetailRow[];
  message?: string;
  stackTrace?: string;
  attributes?: TelemetryDetailRow[];
}

@Component({
  selector: 'app-telemetry-details-dialog',
  imports: [CommonModule, IButton, ICard],
  template: `
    <div class="error-details">
      @if (data.message) {
        <i-card title="Error message">
          <i-button icon="pi pi-copy" severity="secondary" (clicked)="copy(data.message)"
            >Copy message</i-button
          >
          <p class="message">{{ data.message }}</p>
        </i-card>
      }
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
      @if (data.stackTrace) {
        <i-card title="Stack trace">
          <div class="stack-actions">
            <i-button icon="pi pi-copy" severity="secondary" (clicked)="copy(data.stackTrace)"
              >Copy stack trace</i-button
            >
            <i-button severity="secondary" (clicked)="wrapStack.set(!wrapStack())">{{
              wrapStack() ? 'Show original lines' : 'Wrap long lines'
            }}</i-button>
          </div>
          <pre
            class="stack-trace"
            [class.wrap]="wrapStack()"
            tabindex="0"
            aria-label="Full stack trace"
            >{{ data.stackTrace }}</pre
          >
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
      <p role="status" aria-live="polite">{{ copyStatus() }}</p>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.Eager,
  styles: [
    `
      .error-details {
        display: grid;
        gap: 1rem;
        min-width: 0;
        .message {
          white-space: pre-wrap;
          overflow-wrap: anywhere;
          line-height: 1.6;
        }
        .stack-actions {
          display: flex;
          flex-wrap: wrap;
          gap: 0.5rem;
        }
        .stack-trace {
          padding: 1rem;
          border-radius: 0.5rem;
          background: var(--surface-ground);
          border: 1px solid var(--surface-border);
          overflow: auto;
          max-height: 28rem;
          font:
            0.85rem/1.7 ui-monospace,
            Consolas,
            monospace;
          tab-size: 4;
          &.wrap {
            white-space: pre-wrap;
            overflow-wrap: anywhere;
          }
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
  protected readonly wrapStack = signal(true);
  protected readonly copyStatus = signal('');

  protected async copy(value: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(value);
      this.copyStatus.set('Copied to clipboard.');
    } catch {
      this.copyStatus.set('Copy unavailable. Select the text and copy it manually.');
    }
  }
}

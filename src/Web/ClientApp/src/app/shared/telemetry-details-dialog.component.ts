import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';

export interface TelemetryDetailRow {
  label: string;
  value: string;
}

export interface TelemetryDetailsDialogData {
  details: TelemetryDetailRow[];
}

@Component({
  selector: 'app-telemetry-details-dialog',
  imports: [CommonModule],
  template: `
    <dl class="telemetry-details">
      @for (detail of data.details; track detail.label) {
        <div>
          <dt>{{ detail.label }}</dt>
          <dd>{{ detail.value }}</dd>
        </div>
      }
    </dl>
  `,
  styles: [
    `
      .telemetry-details {
        display: grid;
        gap: 0.75rem;
        margin: 0;
      }

      .telemetry-details div {
        border-bottom: 1px solid;
        display: grid;
        gap: 0.25rem;
        padding-bottom: 0.75rem;
      }

      .telemetry-details div:last-child {
        border-bottom: 0;
        padding-bottom: 0;
      }

      dd {
        margin: 0;
        overflow-wrap: anywhere;
        white-space: pre-wrap;
      }
    `,
  ],
})
export class TelemetryDetailsDialogComponent {
  data: TelemetryDetailsDialogData = { details: [] };
}

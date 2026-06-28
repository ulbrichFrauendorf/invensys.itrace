import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  DialogService,
  GridData,
  IButton,
  ICard,
  IChart,
  ISelect,
  ITable,
  TooltipDirective,
} from 'invensys-ng';
import { ApplicationContextService } from '../../core/application-context.service';
import { ITraceApiService, TelemetryEventDto } from '../../core/itrace-api.service';
import {
  formatDateTime,
  formatMilliseconds,
} from '../../shared/application-selector';
import {
  buildGroupRows,
  buildTelemetryRows,
  buildVolumeChart,
  filterByTimeRange,
  TelemetryGroupRow,
  TelemetryRow,
  telemetryTimeRanges,
  TimeRangeOption,
} from '../../shared/telemetry-table-helpers';
import { TelemetryDetailsDialogComponent } from '../../shared/telemetry-details-dialog.component';

@Component({
  selector: 'app-request-durations',
  imports: [CommonModule, FormsModule, IButton, ICard, IChart, ISelect, ITable, TooltipDirective],
  templateUrl: './request-durations.html',
  styleUrl: './request-durations.scss',
})
export class RequestDurations {
  private readonly api = inject(ITraceApiService);
  private readonly applicationContext = inject(ApplicationContextService);
  private readonly dialogService = inject(DialogService);

  protected readonly events = signal<TelemetryEventDto[]>([]);
  protected readonly timeRangeOptions = telemetryTimeRanges;
  protected selectedTimeRange: TimeRangeOption = this.timeRangeOptions[2];

  protected readonly filteredEvents = computed(() =>
    filterByTimeRange(this.events(), this.selectedTimeRange),
  );

  protected readonly chart = computed(() =>
    buildVolumeChart(
      'request-durations-volume',
      'Requests',
      this.filteredEvents(),
      this.selectedTimeRange,
    ),
  );

  protected readonly rows = computed(() =>
    buildTelemetryRows(
      this.filteredEvents(),
      (event) => this.hint(event),
      (event) => this.groupKey(event),
    ),
  );

  protected readonly groupGrid = computed<GridData<TelemetryGroupRow, TelemetryRow>>(() => ({
    columns: [
      { field: 'groupKey', header: 'Group', sortable: true },
      { field: 'samples', header: 'Samples', type: 'number', sortable: true },
      { field: 'latest', header: 'Latest', sortable: true },
      { field: 'hint', header: 'Latest hint', sortable: true },
      { field: 'averageDuration', header: 'Avg duration', sortable: true },
      { field: 'p95Duration', header: 'P95 duration', sortable: true },
    ],
    rows: buildGroupRows(this.rows()),
    details: {
      columns: [
        { field: 'occurred', header: 'Occurred', sortable: true },
        { field: 'applicationName', header: 'Application', sortable: true },
        { field: 'siteName', header: 'Site', sortable: true },
        { field: 'method', header: 'Method', sortable: true },
        { field: 'hint', header: 'Request hint', sortable: true },
        { field: 'route', header: 'Route', sortable: true },
        { field: 'statusCode', header: 'Status', type: 'number', sortable: true },
        { field: 'duration', header: 'Duration', sortable: true },
      ],
      rows: (group) => this.rows().filter((row) => row.groupKey === group.groupKey),
      actions: [
        {
          id: 'view-details',
          icon: 'pi pi-eye',
          tooltip: 'View details',
          handler: (event) => this.showDetails(event),
        },
      ],
    },
  }));

  constructor() {
    effect((onCleanup) => {
      const subscription = this.load(this.applicationContext.selectedApplicationId());
      onCleanup(() => subscription.unsubscribe());
    });
  }

  protected load(applicationId = this.applicationContext.selectedApplicationId()) {
    return this.api.getRequestDurations(applicationId).subscribe((response) => {
      this.events.set(response.items ?? []);
    });
  }

  protected selectTimeRange(option: TimeRangeOption): void {
    this.selectedTimeRange = option;
  }

  private groupKey(event: TelemetryEventDto): string {
    return (
      [event.method, event.route ?? event.operation, event.statusCode].filter(Boolean).join(' ') ||
      'Unclassified request'
    );
  }

  private hint(event: TelemetryEventDto): string {
    return (
      [
        event.method,
        event.route ?? event.operation,
        event.statusCode ? `HTTP ${event.statusCode}` : undefined,
      ]
        .filter(Boolean)
        .join(' ') ||
      event.traceId ||
      'No request hint captured'
    );
  }

  protected showDetails(event: TelemetryEventDto): void {
    this.dialogService.open(TelemetryDetailsDialogComponent, {
      header: 'Request entry details',
      width: '42rem',
      dismissableMask: true,
      breakpoints: { '960px': { width: '90vw' }, '640px': { width: '96vw' } },
      contentStyle: { 'max-height': '70vh', overflow: 'auto' },
      data: {
        details: this.detailRows([
          ['Occurred', formatDateTime(event.occurredAt)],
          ['Application', event.applicationName],
          ['Environment', event.environment],
          ['Site', event.siteName],
          ['Method', event.method],
          ['Route', event.route],
          ['Status', event.statusCode],
          ['Duration', formatMilliseconds(event.durationMs)],
          ['Trace', event.traceId],
          ['Span', event.spanId],
        ]),
      },
    });
  }

  private detailRows(rows: Array<[string, string | number | null | undefined]>) {
    return rows
      .filter(([, value]) => value !== null && value !== undefined && value !== '')
      .map(([label, value]) => ({ label, value: String(value) }));
  }
}

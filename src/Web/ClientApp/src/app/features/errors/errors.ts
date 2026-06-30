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
import { formatDateTime } from '../../shared/application-selector';
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
  selector: 'app-errors',
  imports: [CommonModule, FormsModule, IButton, ICard, IChart, ISelect, ITable, TooltipDirective],
  templateUrl: './errors.html',
  styleUrl: './errors.scss',
})
export class Errors {
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
    buildVolumeChart('errors-volume', 'Errors', this.filteredEvents(), this.selectedTimeRange),
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
    ],
    rows: buildGroupRows(this.rows()),
    details: {
      columns: [
        { field: 'occurred', header: 'Occurred', sortable: true },
        { field: 'severity', header: 'Severity', sortable: true },
        { field: 'hint', header: 'Issue hint', sortable: true },
        { field: 'applicationName', header: 'Application', sortable: true },
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
    return this.api.getErrors(applicationId).subscribe((response) => {
      this.events.set(response.items ?? []);
    });
  }

  protected selectTimeRange(option: TimeRangeOption): void {
    this.selectedTimeRange = option;
  }

  private groupKey(event: TelemetryEventDto): string {
    return (
      [event.exceptionType, event.operation, event.route]
        .filter(Boolean)
        .join(' / ') || 'Unclassified error'
    );
  }

  private hint(event: TelemetryEventDto): string {
    return (
      event.message ||
      event.exceptionType ||
      event.operation ||
      event.route ||
      event.traceId ||
      'No error hint captured'
    );
  }

  protected showDetails(event: TelemetryEventDto): void {
    this.dialogService.open(TelemetryDetailsDialogComponent, {
      header: 'Error entry details',
      width: '42rem',
      dismissableMask: true,
      breakpoints: { '960px': { width: '90vw' }, '640px': { width: '96vw' } },
      contentStyle: { 'max-height': '70vh', overflow: 'auto' },
      data: {
        details: this.detailRows([
          ['Occurred', formatDateTime(event.occurredAt)],
          ['Severity', event.severity],
          ['Application', event.applicationName],
          ['Environment', event.environment],
          ['Exception', event.exceptionType],
          ['Message', event.message],
          ['Operation', event.operation],
          ['Route', event.route],
          ['Method', event.method],
          ['Status', event.statusCode],
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

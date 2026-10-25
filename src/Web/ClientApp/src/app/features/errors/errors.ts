import { PageLayoutComponent, PageHeaderActionsDirective } from 'invensys-ng';
import { CommonModule } from '@angular/common';
import {
  Component,
  computed,
  effect,
  inject,
  signal,
  ChangeDetectionStrategy,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  DialogService,
  GridData,
  IButton,
  ICard,
  IChart,
  IInputText,
  ISelect,
  ITable,
  TooltipDirective,
  WhisperService,
} from 'invensys-ng';
import { ApplicationContextService } from '../../core/application-context.service';
import { ITraceApiService, TelemetryEventDto } from '../../core/itrace-api.service';
import {
  buildTelemetryRows,
  buildVolumeChart,
  filterByTimeRange,
  TelemetryRow,
  telemetryTimeRanges,
  TimeRangeOption,
} from '../../shared/telemetry-table-helpers';
import { TelemetryDetailsDialogComponent } from '../../shared/telemetry-details-dialog.component';

@Component({
  selector: 'app-errors',
  imports: [
    PageLayoutComponent,
    PageHeaderActionsDirective,
    CommonModule,
    FormsModule,
    IButton,
    ICard,
    IChart,
    IInputText,
    ISelect,
    ITable,
    TooltipDirective,
  ],
  templateUrl: './errors.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './errors.scss',
})
export class Errors {
  private readonly api = inject(ITraceApiService);
  private readonly applicationContext = inject(ApplicationContextService);
  private readonly dialogService = inject(DialogService);
  private readonly whisperService = inject(WhisperService);

  protected readonly events = signal<TelemetryEventDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected traceIdLookup = '';
  protected readonly traceLookupLoading = signal(false);
  protected readonly timeRangeOptions = telemetryTimeRanges;
  protected selectedTimeRange: TimeRangeOption = this.timeRangeOptions[2];
  private readonly timeRange = signal(this.selectedTimeRange);

  protected readonly filteredEvents = computed(() =>
    filterByTimeRange(this.events(), this.timeRange()),
  );

  protected readonly chart = computed(() =>
    buildVolumeChart('errors-volume', 'Errors', this.filteredEvents(), this.timeRange()),
  );

  protected readonly rows = computed(() => {
    const occurrences = new Map<string, TelemetryRow & { count: number }>();
    for (const row of buildTelemetryRows(
      this.filteredEvents(),
      (event) => this.hint(event),
      (event) => this.groupKey(event),
    )) {
      const previous = occurrences.get(row.groupKey);
      const count = (previous?.count ?? 0) + 1;
      const latest =
        previous && Date.parse(previous.occurredAt!) > Date.parse(row.occurredAt!) ? previous : row;
      occurrences.set(row.groupKey, { ...latest, count });
    }
    return [...occurrences.values()].sort(
      (a, b) => Date.parse(b.occurredAt!) - Date.parse(a.occurredAt!),
    );
  });

  protected readonly eventGrid = computed<GridData<TelemetryRow & { count: number }>>(() => ({
    columns: [
      { field: 'occurred', header: 'Last occurred', sortable: true },
      { field: 'severity', header: 'Severity', sortable: true, severity: 'danger' },
      { field: 'hint', header: 'Message', sortable: true, width: '24rem' },
      { field: 'applicationName', header: 'Application', sortable: true },
      { field: 'environment', header: 'Environment', sortable: true },
      { field: 'method', header: 'Method', sortable: true },
      { field: 'route', header: 'Route', sortable: true },
      { field: 'statusCode', header: 'HTTP status', type: 'number', sortable: true },
      { field: 'count', header: 'Count', type: 'number', sortable: true },
    ],
    rows: this.rows(),
    actions: [
      {
        id: 'view-error',
        icon: 'pi pi-eye',
        label: 'View latest',
        tooltip: 'View the latest occurrence',
        handler: (row) => this.showDetails(row),
      },
    ],
  }));

  constructor() {
    effect((onCleanup) => {
      const subscription = this.load(
        this.applicationContext.selectedApplicationId(),
        this.timeRange(),
      );
      onCleanup(() => subscription.unsubscribe());
    });
  }

  protected load(
    applicationId = this.applicationContext.selectedApplicationId(),
    range = this.timeRange(),
  ) {
    this.loading.set(true);
    this.loadError.set(null);
    return this.api
      .getErrors(applicationId, range.value / 60_000)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => this.events.set(response.items ?? []),
        error: () => {
          const detail =
            'Unable to load errors. Check your connection and iServe access, then refresh.';
          this.loadError.set(detail);
          this.whisperService.add({ severity: 'warning', summary: 'Error log', detail });
        },
      });
  }

  protected lookupTraceId(): void {
    const traceId = this.traceIdLookup.trim();

    if (!traceId) {
      this.whisperService.add({
        severity: 'warning',
        summary: 'Trace lookup',
        detail: 'Enter a trace ID to search.',
      });
      return;
    }

    this.traceLookupLoading.set(true);
    this.api
      .lookupErrorByTraceId(traceId, this.applicationContext.selectedApplicationId())
      .pipe(finalize(() => this.traceLookupLoading.set(false)))
      .subscribe({
        next: (event) => this.showDetails(event),
        error: () =>
          this.whisperService.add({
            severity: 'warning',
            summary: 'Trace lookup',
            detail: 'No error was found for that trace ID, or the lookup could not be completed.',
          }),
      });
  }

  protected selectTimeRange(option: TimeRangeOption): void {
    this.selectedTimeRange = option;
    this.timeRange.set(option);
  }

  private groupKey(event: TelemetryEventDto): string {
    return JSON.stringify([
      event.applicationId,
      event.environment,
      event.exceptionType,
      event.message,
      event.stackTrace,
    ]);
  }

  private hint(event: TelemetryEventDto): string {
    return (
      event.message?.replace(/\s+/g, ' ').slice(0, 220) ||
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
      width: 'min(70rem, 94vw)',
      dismissableMask: true,
      contentStyle: { 'max-height': '70vh', overflow: 'auto' },
      data: {
        stackTrace: event.stackTrace || 'No stack trace was captured for this error.',
        stackTraceOnly: true,
      },
    });
  }
}

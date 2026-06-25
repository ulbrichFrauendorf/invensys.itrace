import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DialogService, GridData, IButton, ICard, ISelect, ITable, TooltipDirective } from 'invensys-ng';
import {
  ApplicationDto,
  ITraceApiService,
  TelemetryEventDto,
} from '../../core/itrace-api.service';
import {
  ApplicationOption,
  formatDateTime,
  formatMilliseconds,
  toApplicationOptions,
} from '../../shared/application-selector';
import { TelemetryDetailsDialogComponent } from '../../shared/telemetry-details-dialog.component';

@Component({
  selector: 'app-db-durations',
  imports: [CommonModule, FormsModule, IButton, ICard, ISelect, ITable, TooltipDirective],
  templateUrl: './db-durations.html',
  styleUrl: './db-durations.scss',
})
export class DbDurations implements OnInit {
  private readonly api = inject(ITraceApiService);
  private readonly dialogService = inject(DialogService);

  protected readonly applications = signal<ApplicationDto[]>([]);
  protected readonly events = signal<TelemetryEventDto[]>([]);
  protected selectedApplication: ApplicationOption | null = null;
  protected readonly applicationOptions = computed(() =>
    toApplicationOptions(this.applications()),
  );

  protected readonly grid = computed<GridData<TelemetryEventDto>>(() => ({
    columns: [
      { field: 'occurred', header: 'Occurred', sortable: true },
      { field: 'applicationName', header: 'Application', sortable: true },
      { field: 'siteName', header: 'Site', sortable: true },
      { field: 'operation', header: 'Operation', sortable: true },
      { field: 'database', header: 'Database', sortable: true },
      { field: 'duration', header: 'Duration', sortable: true },
    ],
    rows: this.events().map((event) => ({
      ...event,
      occurred: formatDateTime(event.occurredAt),
      duration: formatMilliseconds(event.durationMs),
    })),
    actions: [
      {
        id: 'view-details',
        icon: 'pi pi-eye',
        tooltip: 'View details',
        handler: (event) => this.showDetails(event),
      },
    ],
  }));

  ngOnInit(): void {
    this.api.getApplications().subscribe((applications) => this.applications.set(applications));
    this.load();
  }

  protected selectApplication(option: ApplicationOption | null): void {
    this.load();
  }

  protected load(): void {
    this.api.getDbDurations(this.selectedApplication?.id).subscribe((response) => {
      this.events.set(response.items ?? []);
    });
  }

  protected showDetails(event: TelemetryEventDto): void {
    this.dialogService.open(TelemetryDetailsDialogComponent, {
      header: 'Database entry details',
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
          ['Operation', event.operation],
          ['Database', event.database],
          ['Provider', event.dbSystem],
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

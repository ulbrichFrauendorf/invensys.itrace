import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ICard, ISelect, ITable, GridData } from 'integra-ng';
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

@Component({
  selector: 'app-db-durations-page',
  imports: [CommonModule, FormsModule, ICard, ISelect, ITable],
  templateUrl: './db-durations.page.html',
  styleUrl: './db-durations.page.scss',
})
export class DbDurationsPage implements OnInit {
  private readonly api = inject(ITraceApiService);

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
      { field: 'dbSystem', header: 'Provider', sortable: true },
      { field: 'duration', header: 'Duration', sortable: true },
      { field: 'traceId', header: 'Trace' },
    ],
    rows: this.events().map((event) => ({
      ...event,
      occurred: formatDateTime(event.occurredAt),
      duration: formatMilliseconds(event.durationMs),
    })),
  }));

  ngOnInit(): void {
    this.api.getApplications().subscribe((applications) => this.applications.set(applications));
    this.load();
  }

  protected selectApplication(option: ApplicationOption | null): void {
    this.selectedApplication = option;
    this.load();
  }

  protected clearApplication(): void {
    this.selectApplication(null);
  }

  protected load(): void {
    this.api.getDbDurations(this.selectedApplication?.id).subscribe((response) => {
      this.events.set(response.items);
    });
  }
}

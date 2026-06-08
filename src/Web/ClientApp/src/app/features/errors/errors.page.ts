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
  toApplicationOptions,
} from '../../shared/application-selector';

@Component({
  selector: 'app-errors-page',
  imports: [CommonModule, FormsModule, ICard, ISelect, ITable],
  templateUrl: './errors.page.html',
  styleUrl: './errors.page.scss',
})
export class ErrorsPage implements OnInit {
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
      { field: 'severity', header: 'Severity', sortable: true },
      { field: 'applicationName', header: 'Application', sortable: true },
      { field: 'siteName', header: 'Site', sortable: true },
      { field: 'exceptionType', header: 'Exception', sortable: true },
      { field: 'message', header: 'Message' },
      { field: 'traceId', header: 'Trace' },
    ],
    rows: this.events().map((event) => ({
      ...event,
      occurred: formatDateTime(event.occurredAt),
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
    this.api.getErrors(this.selectedApplication?.id).subscribe((response) => {
      this.events.set(response.items ?? []);
    });
  }
}

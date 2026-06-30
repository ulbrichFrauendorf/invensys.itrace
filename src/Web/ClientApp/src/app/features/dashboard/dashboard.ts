import { CommonModule } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  GridData,
  IButton,
  ICard,
  IChart,
  IChartData,
  ISelect,
  ITable,
  ITag,
  NoContentComponent,
  TooltipDirective,
} from 'invensys-ng';
import {
  DashboardDto,
  ITraceApiService,
  ApplicationHealthDto,
} from '../../core/itrace-api.service';
import { ApplicationContextService } from '../../core/application-context.service';
import {
  formatDateTime,
  formatMilliseconds,
} from '../../shared/application-selector';
import { telemetryTimeRanges, TimeRangeOption } from '../../shared/telemetry-table-helpers';

type TagSeverity = 'success' | 'warning' | 'danger';

@Component({
  selector: 'app-dashboard',
  imports: [
    CommonModule,
    FormsModule,
    IButton,
    ICard,
    IChart,
    ISelect,
    ITable,
    ITag,
    NoContentComponent,
    TooltipDirective,
  ],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly api = inject(ITraceApiService);
  private readonly applicationContext = inject(ApplicationContextService);

  protected readonly dashboard = signal<DashboardDto | null>(null);
  protected readonly loading = signal(false);
  protected readonly timeRangeOptions = telemetryTimeRanges;
  protected selectedTimeRange: TimeRangeOption = this.timeRangeOptions[2];

  protected readonly charts = computed<IChartData[]>(() => {
    const dashboard = this.dashboard();
    const applications = dashboard?.applications ?? [];
    if (applications.length === 0) {
      return [];
    }

    return [
      {
        chartId: 'application-health',
        chartType: 'bar',
        labels: applications.map((application) => application.applicationName ?? 'Unknown'),
        dataSets: [
          {
            label: 'Request p95',
            data: applications.map((application) => application.requestsInWindow?.p95Ms ?? 0),
            backgroundColors: applications.map(() => '--cyan-500'),
          },
          {
            label: 'DB p95',
            data: applications.map((application) => application.databaseInWindow?.p95Ms ?? 0),
            backgroundColors: applications.map(() => '--orange-500'),
          },
        ],
      },
    ];
  });

  protected readonly applicationGrid = computed<GridData<ApplicationHealthDto>>(() => ({
    columns: [
      { field: 'status', header: 'Status', sortable: true },
      { field: 'applicationName', header: 'Application', sortable: true },
      { field: 'environment', header: 'Environment', sortable: true },
      { field: 'lastSeen', header: 'Last seen', sortable: true },
      { field: 'errorsInWindow', header: 'Errors', type: 'number', sortable: true },
      { field: 'requestP95', header: 'Request p95', sortable: true },
      { field: 'dbP95', header: 'DB p95', sortable: true },
    ],
    rows: (this.dashboard()?.applications ?? []).map((application) => ({
      ...application,
      lastSeen: formatDateTime(application.lastSeenAt),
      requestP95: formatMilliseconds(application.requestsInWindow?.p95Ms),
      dbP95: formatMilliseconds(application.databaseInWindow?.p95Ms),
    })),
  }));

  constructor() {
    effect((onCleanup) => {
      const subscription = this.loadDashboard(this.applicationContext.selectedApplicationId());
      onCleanup(() => subscription.unsubscribe());
    });
  }

  protected refresh(): void {
    this.loadDashboard();
  }

  protected selectTimeRange(option: TimeRangeOption): void {
    this.selectedTimeRange = option;
    this.loadDashboard();
  }

  protected formatMilliseconds(value?: number | null): string {
    return formatMilliseconds(value);
  }

  protected statusSeverity(application: ApplicationHealthDto): TagSeverity {
    const status = (application.status ?? 'offline').toLowerCase();

    if (status === 'healthy') {
      return 'success';
    }

    if (status === 'degraded') {
      return 'warning';
    }

    return 'danger';
  }

  private loadDashboard(applicationId = this.applicationContext.selectedApplicationId()) {
    this.loading.set(true);
    return this.api.getDashboard(applicationId, this.selectedTimeRange.value / 60_000).subscribe({
      next: (dashboard) => this.dashboard.set(dashboard),
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }
}

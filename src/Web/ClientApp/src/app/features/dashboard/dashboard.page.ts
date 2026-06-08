import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IButton, ICard, IChart, ISelect, ITable, GridData, IChartData } from 'integra-ng';
import {
  ApplicationDto,
  DashboardDto,
  ITraceApiService,
  SiteHealthDto,
} from '../../core/itrace-api.service';
import {
  ApplicationOption,
  formatDateTime,
  formatMilliseconds,
  toApplicationOptions,
} from '../../shared/application-selector';

@Component({
  selector: 'app-dashboard-page',
  imports: [CommonModule, FormsModule, IButton, ICard, IChart, ISelect, ITable],
  templateUrl: './dashboard.page.html',
  styleUrl: './dashboard.page.scss',
})
export class DashboardPage implements OnInit {
  private readonly api = inject(ITraceApiService);

  protected readonly applications = signal<ApplicationDto[]>([]);
  protected readonly dashboard = signal<DashboardDto | null>(null);
  protected readonly loading = signal(false);
  protected selectedApplication: ApplicationOption | null = null;

  protected readonly applicationOptions = computed(() =>
    toApplicationOptions(this.applications()),
  );

  protected readonly charts = computed<IChartData[]>(() => {
    const dashboard = this.dashboard();
    const sites = dashboard?.sites ?? [];
    if (sites.length === 0) {
      return [];
    }

    return [
      {
        chartId: 'site-health',
        chartType: 'bar',
        labels: sites.map((site) => site.siteName ?? 'Unknown'),
        dataSets: [
          {
            label: 'Request p95',
            data: sites.map((site) => site.requests24h?.p95Ms ?? 0),
            backgroundColors: sites.map(() => '--cyan-500'),
          },
          {
            label: 'DB p95',
            data: sites.map((site) => site.database24h?.p95Ms ?? 0),
            backgroundColors: sites.map(() => '--orange-500'),
          },
        ],
      },
    ];
  });

  protected readonly siteGrid = computed<GridData<SiteHealthDto>>(() => ({
    columns: [
      { field: 'status', header: 'Status', sortable: true },
      { field: 'applicationName', header: 'Application', sortable: true },
      { field: 'environment', header: 'Environment', sortable: true },
      { field: 'siteName', header: 'Site', sortable: true },
      { field: 'lastSeen', header: 'Last seen', sortable: true },
      { field: 'errors24h', header: 'Errors', type: 'number', sortable: true },
      { field: 'requestP95', header: 'Request p95', sortable: true },
      { field: 'dbP95', header: 'DB p95', sortable: true },
    ],
    rows: (this.dashboard()?.sites ?? []).map((site) => ({
      ...site,
      lastSeen: formatDateTime(site.lastSeenAt),
      requestP95: formatMilliseconds(site.requests24h?.p95Ms),
      dbP95: formatMilliseconds(site.database24h?.p95Ms),
    })),
  }));

  ngOnInit(): void {
    this.loadApplications();
    this.loadDashboard();
  }

  protected selectApplication(option: ApplicationOption | null): void {
    this.loadDashboard(option?.id ?? null);
  }

  protected refresh(): void {
    this.loadDashboard(this.selectedApplication?.id ?? null);
  }

  protected formatMilliseconds(value?: number | null): string {
    return formatMilliseconds(value);
  }

  protected statusClass(site: SiteHealthDto): string {
    return `health-pill health-pill--${(site.status ?? 'offline').toLowerCase()}`;
  }

  private loadApplications(): void {
    this.api.getApplications().subscribe((applications) => {
      this.applications.set(applications);
    });
  }

  private loadDashboard(applicationId?: string | null): void {
    this.loading.set(true);
    this.api.getDashboard(applicationId).subscribe({
      next: (dashboard) => this.dashboard.set(dashboard),
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }
}

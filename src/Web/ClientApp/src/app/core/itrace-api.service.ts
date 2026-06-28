import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApplicationsClient,
  DashboardClient,
  DbDurationsClient,
  ErrorsClient,
  PerformanceCountersClient,
  RequestDurationsClient,
} from '../web-api-client';
import type {
  ApplicationDto,
  DashboardDto,
  PerformanceCounterDailySummaryDto,
  PerformanceCounterSeriesDto,
  PerformanceCounterSourceDto,
  PerformanceCountersDto,
  RegisterApplicationRequest,
  TelemetryEventDto,
  TelemetryListResponse,
} from '../web-api-client';

export {
  SiteHealthStatus,
  TelemetrySignal,
} from '../web-api-client';

export type {
  ApplicationDto,
  DashboardDto,
  MetricSummaryDto,
  PerformanceCounterDailySummaryDto,
  PerformanceCounterSeriesDto,
  PerformanceCounterSourceDto,
  PerformanceCountersDto,
  RegisterApplicationRequest,
  SiteHealthDto,
  TelemetryEventDto,
  TelemetryListResponse,
} from '../web-api-client';

@Injectable({ providedIn: 'root' })
export class ITraceApiService {
  private readonly applications = inject(ApplicationsClient);
  private readonly dashboard = inject(DashboardClient);
  private readonly errors = inject(ErrorsClient);
  private readonly performanceCounters = inject(PerformanceCountersClient);
  private readonly requestDurations = inject(RequestDurationsClient);
  private readonly dbDurations = inject(DbDurationsClient);

  getApplications(): Observable<ApplicationDto[]> {
    return this.applications.listApplications();
  }

  registerApplication(request: RegisterApplicationRequest): Observable<ApplicationDto> {
    return this.applications.registerApplication(request);
  }

  getDashboard(applicationId?: string | null, windowMinutes?: number | null): Observable<DashboardDto> {
    return this.dashboard.getDashboard(applicationId, windowMinutes);
  }

  getErrors(applicationId?: string | null): Observable<TelemetryListResponse> {
    return this.errors.listErrors(applicationId, 200);
  }

  getRequestDurations(applicationId?: string | null): Observable<TelemetryListResponse> {
    return this.requestDurations.listRequestDurations(applicationId, 200);
  }

  getDbDurations(applicationId?: string | null): Observable<TelemetryListResponse> {
    return this.dbDurations.listDbDurations(applicationId, 200);
  }

  getPerformanceCounters(intervalMinutes?: number | null): Observable<PerformanceCountersDto> {
    return this.performanceCounters.getPerformanceCounters(intervalMinutes);
  }
}

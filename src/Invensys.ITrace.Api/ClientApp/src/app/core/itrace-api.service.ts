import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export type TelemetrySignal = 'Error' | 'RequestDuration' | 'DbDuration';
export type SiteHealthStatus = 'Healthy' | 'Degraded' | 'Offline';

export interface RegisterApplicationRequest {
  name: string;
  environment: string;
  siteName: string;
  description?: string | null;
}

export interface ApplicationDto {
  id: string;
  name: string;
  environment: string;
  siteName: string;
  dsn: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
  description?: string | null;
}

export interface MetricSummaryDto {
  count: number;
  averageMs: number;
  p95Ms: number;
  maxMs: number;
}

export interface SiteHealthDto {
  applicationId: string;
  applicationName: string;
  environment: string;
  siteName: string;
  status: SiteHealthStatus;
  lastSeenAt?: string | null;
  errors24h: number;
  requests24h: MetricSummaryDto;
  database24h: MetricSummaryDto;
}

export interface DashboardDto {
  selectedApplicationId?: string | null;
  windowStart: string;
  windowEnd: string;
  applicationCount: number;
  errorCount: number;
  requests: MetricSummaryDto;
  database: MetricSummaryDto;
  sites: SiteHealthDto[];
}

export interface TelemetryEventDto {
  id: string;
  applicationId: string;
  applicationName: string;
  environment: string;
  siteName: string;
  signal: TelemetrySignal;
  occurredAt: string;
  severity: string;
  message?: string | null;
  operation?: string | null;
  route?: string | null;
  method?: string | null;
  statusCode?: number | null;
  durationMs?: number | null;
  database?: string | null;
  dbSystem?: string | null;
  exceptionType?: string | null;
  traceId?: string | null;
  spanId?: string | null;
  attributes: Record<string, string | null>;
}

export interface TelemetryListResponse {
  items: TelemetryEventDto[];
  count: number;
  take: number;
}

@Injectable({ providedIn: 'root' })
export class ITraceApiService {
  private readonly http = inject(HttpClient);

  getApplications(): Observable<ApplicationDto[]> {
    return this.http.get<ApplicationDto[]>('/api/applications');
  }

  registerApplication(request: RegisterApplicationRequest): Observable<ApplicationDto> {
    return this.http.post<ApplicationDto>('/api/applications', request);
  }

  getDashboard(applicationId?: string | null): Observable<DashboardDto> {
    return this.http.get<DashboardDto>('/api/dashboard', {
      params: this.applicationParams(applicationId),
    });
  }

  getErrors(applicationId?: string | null): Observable<TelemetryListResponse> {
    return this.getTelemetry('/api/errors', applicationId);
  }

  getRequestDurations(applicationId?: string | null): Observable<TelemetryListResponse> {
    return this.getTelemetry('/api/request-durations', applicationId);
  }

  getDbDurations(applicationId?: string | null): Observable<TelemetryListResponse> {
    return this.getTelemetry('/api/db-durations', applicationId);
  }

  private getTelemetry(path: string, applicationId?: string | null): Observable<TelemetryListResponse> {
    return this.http.get<TelemetryListResponse>(path, {
      params: this.applicationParams(applicationId).set('take', 200),
    });
  }

  private applicationParams(applicationId?: string | null): HttpParams {
    let params = new HttpParams();
    if (applicationId) {
      params = params.set('applicationId', applicationId);
    }

    return params;
  }
}

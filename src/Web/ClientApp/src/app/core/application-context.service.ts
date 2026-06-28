import { computed, inject, Injectable, signal } from '@angular/core';
import { ApplicationOption, toApplicationOptions } from '../shared/application-selector';
import { ApplicationDto, ITraceApiService } from './itrace-api.service';

@Injectable({ providedIn: 'root' })
export class ApplicationContextService {
  private readonly api = inject(ITraceApiService);

  readonly applications = signal<ApplicationDto[]>([]);
  readonly selectedApplication = signal<ApplicationOption | null>(null);
  readonly applicationOptions = computed(() => toApplicationOptions(this.applications()));
  readonly selectedApplicationId = computed(() => this.selectedApplication()?.id ?? null);

  constructor() {
    this.refreshApplications();
  }

  refreshApplications(): void {
    this.api.getApplications().subscribe((applications) => this.applications.set(applications));
  }

  selectApplication(application: ApplicationOption | null): void {
    this.selectedApplication.set(application);
  }
}

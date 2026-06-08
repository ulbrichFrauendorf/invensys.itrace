import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { IButton, ICard, IInputText, ITable, GridData } from 'integra-ng';
import {
  ApplicationDto,
  ITraceApiService,
  RegisterApplicationRequest,
} from '../../core/itrace-api.service';
import { formatDateTime } from '../../shared/application-selector';

@Component({
  selector: 'app-applications-page',
  imports: [CommonModule, ReactiveFormsModule, IButton, ICard, IInputText, ITable],
  templateUrl: './applications.page.html',
  styleUrl: './applications.page.scss',
})
export class ApplicationsPage implements OnInit {
  private readonly api = inject(ITraceApiService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly applications = signal<ApplicationDto[]>([]);
  protected readonly saving = signal(false);
  protected readonly lastCreatedDsn = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(160)]],
    environment: ['Production', [Validators.required, Validators.maxLength(80)]],
    siteName: ['', [Validators.required, Validators.maxLength(160)]],
    description: [''],
  });

  protected readonly grid = computed<GridData<ApplicationDto>>(() => ({
    columns: [
      { field: 'name', header: 'Application', sortable: true },
      { field: 'environment', header: 'Environment', sortable: true },
      { field: 'siteName', header: 'Site', sortable: true },
      { field: 'created', header: 'Created', sortable: true },
      { field: 'dsn', header: 'DSN' },
    ],
    rows: this.applications().map((application) => ({
      ...application,
      created: formatDateTime(application.createdAt),
    })),
    actions: [
      {
        id: 'copy-dsn',
        icon: 'pi pi-copy',
        tooltip: 'Copy DSN',
        handler: (application) => this.copyDsn(application.dsn),
      },
    ],
  }));

  ngOnInit(): void {
    this.loadApplications();
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const formValue = this.form.getRawValue();
    const request: RegisterApplicationRequest = {
      name: formValue.name,
      environment: formValue.environment,
      siteName: formValue.siteName,
      description: formValue.description || undefined,
    };

    this.api.registerApplication(request).subscribe({
      next: (application) => {
        this.lastCreatedDsn.set(application.dsn ?? null);
        this.form.reset({
          name: '',
          environment: 'Production',
          siteName: '',
          description: '',
        });
        this.loadApplications();
      },
      complete: () => this.saving.set(false),
      error: () => this.saving.set(false),
    });
  }

  protected copyLastDsn(): void {
    const dsn = this.lastCreatedDsn();
    if (dsn) {
      this.copyDsn(dsn);
    }
  }

  private loadApplications(): void {
    this.api.getApplications().subscribe((applications) => {
      this.applications.set(applications);
    });
  }

  private copyDsn(dsn?: string): void {
    if (dsn) {
      void navigator.clipboard?.writeText(dsn);
    }
  }
}

import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import {
  GridData,
  IButton,
  ICard,
  IInputText,
  ITable,
  NoContentComponent,
} from 'invensys-ng';
import {
  ApplicationDto,
  ITraceApiService,
  RegisterApplicationRequest,
} from '../../core/itrace-api.service';
import { ApplicationContextService } from '../../core/application-context.service';
import { formatDateTime } from '../../shared/application-selector';

@Component({
  selector: 'app-applications',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    IButton,
    ICard,
    IInputText,
    ITable,
    NoContentComponent,
  ],
  templateUrl: './applications.html',
  styleUrl: './applications.scss',
})
export class Applications {
  private readonly api = inject(ITraceApiService);
  private readonly applicationContext = inject(ApplicationContextService);
  private readonly formBuilder = inject(FormBuilder);

  protected readonly applications = this.applicationContext.applications;
  protected readonly saving = signal(false);
  protected readonly lastCreatedDsn = signal<string | null>(null);

  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(160)]],
    environment: ['Production', [Validators.required, Validators.maxLength(80)]],
    description: [''],
  });

  protected readonly grid = computed<GridData<ApplicationDto>>(() => ({
    columns: [
      { field: 'name', header: 'Application', sortable: true },
      { field: 'environment', header: 'Environment', sortable: true },
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
      description: formValue.description || undefined,
    };

    this.api.registerApplication(request).subscribe({
      next: (application) => {
        this.lastCreatedDsn.set(application.dsn ?? null);
        this.form.reset({
          name: '',
          environment: 'Production',
          description: '',
        });
        this.applicationContext.refreshApplications();
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

  private copyDsn(dsn?: string): void {
    if (dsn) {
      void navigator.clipboard?.writeText(dsn);
    }
  }
}

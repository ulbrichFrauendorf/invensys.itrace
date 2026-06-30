import { ApplicationDto } from '../core/itrace-api.service';

export interface ApplicationOption {
  id: string;
  label: string;
}

export function toApplicationOptions(applications: ApplicationDto[]): ApplicationOption[] {
  return applications
    .filter((application): application is ApplicationDto & { id: string } => !!application.id)
    .map((application) => ({
      id: application.id,
      label: `${application.name ?? 'Unknown'} / ${application.environment ?? 'Unknown'}`,
    }));
}

export function formatDateTime(value?: string | null): string {
  if (!value) {
    return 'Never';
  }

  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

export function formatMilliseconds(value?: number | null): string {
  if (value === null || value === undefined) {
    return '0 ms';
  }

  return `${value.toLocaleString(undefined, { maximumFractionDigits: 2 })} ms`;
}

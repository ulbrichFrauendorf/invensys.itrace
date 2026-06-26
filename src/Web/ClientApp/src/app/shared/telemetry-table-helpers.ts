import { IChartData } from 'invensys-ng';
import { TelemetryEventDto } from '../core/itrace-api.service';
import { formatDateTime, formatMilliseconds } from './application-selector';

export interface TimeRangeOption {
  label: string;
  value: number;
  bucketMinutes: number;
}

export interface TelemetryRow extends TelemetryEventDto {
  occurred: string;
  duration?: string;
  hint: string;
  groupKey: string;
}

export interface TelemetryGroupRow {
  groupKey: string;
  samples: number;
  latest: string;
  hint: string;
  averageDuration?: string;
  p95Duration?: string;
}

export const telemetryTimeRanges: TimeRangeOption[] = [
  { label: '1h', value: 60 * 60 * 1000, bucketMinutes: 5 },
  { label: '4h', value: 4 * 60 * 60 * 1000, bucketMinutes: 15 },
  { label: '24h', value: 24 * 60 * 60 * 1000, bucketMinutes: 60 },
  { label: '1 week', value: 7 * 24 * 60 * 60 * 1000, bucketMinutes: 6 * 60 },
  { label: '1 month', value: 30 * 24 * 60 * 60 * 1000, bucketMinutes: 24 * 60 },
];

export function filterByTimeRange(
  events: TelemetryEventDto[],
  range: TimeRangeOption,
): TelemetryEventDto[] {
  const cutoff = Date.now() - range.value;
  return events.filter((event) => new Date(event.occurredAt ?? 0).getTime() >= cutoff);
}

export function buildTelemetryRows(
  events: TelemetryEventDto[],
  hintSelector: (event: TelemetryEventDto) => string,
  groupSelector: (event: TelemetryEventDto) => string,
): TelemetryRow[] {
  return events.map((event) => ({
    ...event,
    occurred: formatDateTime(event.occurredAt),
    duration: formatMilliseconds(event.durationMs),
    hint: hintSelector(event),
    groupKey: groupSelector(event),
  }));
}

export function buildGroupRows(rows: TelemetryRow[]): TelemetryGroupRow[] {
  const groups = new Map<string, TelemetryRow[]>();
  rows.forEach((row) => groups.set(row.groupKey, [...(groups.get(row.groupKey) ?? []), row]));

  return Array.from(groups.entries())
    .map(([groupKey, items]) => {
      const latest = items.reduce((current, item) =>
        new Date(item.occurredAt ?? 0).getTime() > new Date(current.occurredAt ?? 0).getTime()
          ? item
          : current,
      );
      const durations = items
        .map((item) => item.durationMs)
        .filter((value): value is number => value !== null && value !== undefined)
        .sort((a, b) => a - b);
      const average = durations.length
        ? durations.reduce((sum, value) => sum + value, 0) / durations.length
        : undefined;
      const p95 = durations.length
        ? durations[Math.min(durations.length - 1, Math.ceil(durations.length * 0.95) - 1)]
        : undefined;

      return {
        groupKey,
        samples: items.length,
        latest: formatDateTime(latest.occurredAt),
        hint: latest.hint,
        averageDuration: average === undefined ? undefined : formatMilliseconds(average),
        p95Duration: p95 === undefined ? undefined : formatMilliseconds(p95),
      };
    })
    .sort((a, b) => b.samples - a.samples);
}

export function buildVolumeChart(
  chartId: string,
  label: string,
  events: TelemetryEventDto[],
  range: TimeRangeOption,
): IChartData[] {
  const now = Date.now();
  const start = now - range.value;
  const bucketMs = range.bucketMinutes * 60 * 1000;
  const bucketCount = Math.max(1, Math.ceil(range.value / bucketMs));
  const buckets = Array.from({ length: bucketCount }, (_, index) => ({
    start: start + index * bucketMs,
    count: 0,
  }));

  events.forEach((event) => {
    const occurred = new Date(event.occurredAt ?? 0).getTime();
    const index = Math.floor((occurred - start) / bucketMs);
    if (index >= 0 && index < buckets.length) {
      buckets[index].count += 1;
    }
  });

  return [
    {
      chartId,
      chartType: 'bar',
      labels: buckets.map((bucket) => formatBucket(bucket.start, range)),
      dataSets: [
        { label, data: buckets.map((bucket) => bucket.count), backgroundColors: ['--cyan-500'] },
      ],
    },
  ];
}

function formatBucket(value: number, range: TimeRangeOption): string {
  return new Intl.DateTimeFormat(undefined, {
    month: range.value > 24 * 60 * 60 * 1000 ? 'short' : undefined,
    day: range.value > 24 * 60 * 60 * 1000 ? 'numeric' : undefined,
    hour: '2-digit',
    minute: range.bucketMinutes < 60 ? '2-digit' : undefined,
  }).format(new Date(value));
}

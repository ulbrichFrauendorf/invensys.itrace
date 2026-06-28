import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GridData, IButton, ICard, IChart, IChartData, ISelect, ITable, NoContentComponent, TooltipDirective } from 'invensys-ng';
import {
  ITraceApiService,
  PerformanceCounterDailySummaryDto,
  PerformanceCounterSeriesDto,
  PerformanceCounterSourceDto,
  PerformanceCountersDto,
} from '../../core/itrace-api.service';
import { formatDateTime } from '../../shared/application-selector';

interface IntervalOption {
  label: string;
  value: number;
}

interface ChartPanel {
  title: string;
  charts: IChartData[];
}

type MetricName =
  | 'CpuUsagePercent'
  | 'MemoryUsagePercent'
  | 'DiskUsagePercent'
  | 'NetworkReceiveBytesPerSecond'
  | 'NetworkTransmitBytesPerSecond';

@Component({
  selector: 'app-performance-counters',
  imports: [
    CommonModule,
    FormsModule,
    IButton,
    ICard,
    IChart,
    ISelect,
    ITable,
    NoContentComponent,
    TooltipDirective,
  ],
  templateUrl: './performance-counters.html',
  styleUrl: './performance-counters.scss',
})
export class PerformanceCounters implements OnInit {
  private readonly api = inject(ITraceApiService);

  protected readonly counters = signal<PerformanceCountersDto | null>(null);
  protected readonly loading = signal(false);
  protected readonly intervalOptions: IntervalOption[] = [
    { label: '1 minute', value: 1 },
    { label: '5 minutes', value: 5 },
    { label: '15 minutes', value: 15 },
    { label: '30 minutes', value: 30 },
    { label: '60 minutes', value: 60 },
  ];
  protected selectedInterval: IntervalOption = this.intervalOptions[1];

  protected readonly machineCharts = computed<ChartPanel[]>(() => {
    const series = this.counters()?.series ?? [];
    return [
      this.buildChart('machine-load', 'Machine load', series, ['CpuUsagePercent', 'MemoryUsagePercent', 'DiskUsagePercent'], 'Machine'),
      this.buildChart('machine-network', 'Machine network', series, ['NetworkReceiveBytesPerSecond', 'NetworkTransmitBytesPerSecond'], 'Machine'),
    ].filter((chart): chart is ChartPanel => !!chart);
  });

  protected readonly containerCharts = computed<ChartPanel[]>(() => {
    const series = this.counters()?.series ?? [];
    return [
      this.buildChart('container-load', 'Container load', series, ['CpuUsagePercent', 'MemoryUsagePercent'], 'Container'),
      this.buildChart('container-network', 'Container network', series, ['NetworkReceiveBytesPerSecond', 'NetworkTransmitBytesPerSecond'], 'Container'),
    ].filter((chart): chart is ChartPanel => !!chart);
  });

  protected readonly containerGrid = computed<GridData<PerformanceCounterSourceDto>>(() => ({
    columns: [
      { field: 'sourceName', header: 'Container', sortable: true },
      { field: 'lastSeen', header: 'Last seen', sortable: true },
      { field: 'cpu', header: 'CPU', sortable: true },
      { field: 'memory', header: 'Memory', sortable: true },
      { field: 'rx', header: 'RX', sortable: true },
      { field: 'tx', header: 'TX', sortable: true },
    ],
    rows: (this.counters()?.containers ?? []).map((container) => ({
      ...container,
      lastSeen: formatDateTime(container.lastSeenAt),
      cpu: this.formatPercent(container.cpuUsagePercent),
      memory: `${this.formatPercent(container.memoryUsagePercent)} (${this.formatBytes(container.memoryUsedBytes)})`,
      rx: this.formatRate(container.networkReceiveBytesPerSecond),
      tx: this.formatRate(container.networkTransmitBytesPerSecond),
    })),
  }));

  protected readonly summaryGrid = computed<GridData<PerformanceCounterDailySummaryDto>>(() => ({
    columns: [
      { field: 'day', header: 'Day', sortable: true },
      { field: 'sourceName', header: 'Source', sortable: true },
      { field: 'metricLabel', header: 'Metric', sortable: true },
      { field: 'minimumLabel', header: 'Low', sortable: true },
      { field: 'averageLabel', header: 'Average', sortable: true },
      { field: 'maximumLabel', header: 'High', sortable: true },
      { field: 'alertHighCount', header: 'High alerts', type: 'number', sortable: true },
      { field: 'sampleCount', header: 'Samples', type: 'number', sortable: true },
    ],
    rows: (this.counters()?.dailySummaries ?? []).map((summary) => ({
      ...summary,
      metricLabel: this.metricLabel(summary.metric),
      minimumLabel: this.formatMetric(summary.metric, summary.minimum),
      averageLabel: this.formatMetric(summary.metric, summary.average),
      maximumLabel: this.formatMetric(summary.metric, summary.maximum),
    })),
  }));

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.api.getPerformanceCounters(this.selectedInterval.value).subscribe({
      next: (counters) => this.counters.set(counters),
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }

  protected formatPercent(value?: number | null): string {
    if (value === null || value === undefined) {
      return '0%';
    }

    return `${value.toLocaleString(undefined, { maximumFractionDigits: 1 })}%`;
  }

  protected formatBytes(value?: number | null): string {
    if (!value) {
      return '0 B';
    }

    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let size = value;
    let unit = 0;
    while (size >= 1024 && unit < units.length - 1) {
      size /= 1024;
      unit++;
    }

    return `${size.toLocaleString(undefined, { maximumFractionDigits: unit === 0 ? 0 : 1 })} ${units[unit]}`;
  }

  protected formatRate(value?: number | null): string {
    return `${this.formatBytes(value)} /s`;
  }

  private buildChart(
    chartId: string,
    title: string,
    series: PerformanceCounterSeriesDto[],
    metrics: MetricName[],
    scope: 'Machine' | 'Container',
  ): ChartPanel | null {
    const matching = series.filter((item) => item.scope === scope && metrics.includes(item.metric as MetricName));
    const labels = Array.from(new Set(matching.flatMap((item) => item.points?.map((point) => point.timestamp ?? '') ?? [])))
      .filter(Boolean)
      .sort();

    if (labels.length === 0) {
      return null;
    }

    const colors = ['--cyan-500', '--orange-500', '--green-500', '--indigo-500', '--rose-500', '--amber-500'];

    return {
      title,
      charts: [{
        chartId,
        chartType: 'line',
        labels: labels.map((label) => this.formatTime(label)),
        dataSets: matching.map((item, index) => {
          const values = new Map((item.points ?? []).map((point) => [point.timestamp, point.value ?? 0]));
          return {
            label: `${item.sourceName ?? 'Unknown'} ${this.metricLabel(item.metric)}`,
            data: labels.map((label) => values.get(label) ?? 0),
            backgroundColors: [colors[index % colors.length]],
          };
        }),
      }],
    };
  }

  private formatMetric(metric?: string, value?: number): string {
    if (metric === 'NetworkReceiveBytesPerSecond' || metric === 'NetworkTransmitBytesPerSecond') {
      return this.formatRate(value);
    }

    return this.formatPercent(value);
  }

  private metricLabel(metric?: string): string {
    switch (metric) {
      case 'CpuUsagePercent':
        return 'CPU';
      case 'MemoryUsagePercent':
        return 'Memory';
      case 'DiskUsagePercent':
        return 'Disk';
      case 'NetworkReceiveBytesPerSecond':
        return 'Network RX';
      case 'NetworkTransmitBytesPerSecond':
        return 'Network TX';
      default:
        return 'Unknown';
    }
  }

  private formatTime(value: string): string {
    return new Intl.DateTimeFormat(undefined, {
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(value));
  }
}

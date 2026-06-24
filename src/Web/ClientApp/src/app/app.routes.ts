import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard').then((m) => m.Dashboard),
  },
  {
    path: 'applications',
    loadComponent: () =>
      import('./features/applications/applications').then((m) => m.Applications),
  },
  {
    path: 'errors',
    loadComponent: () =>
      import('./features/errors/errors').then((m) => m.Errors),
  },
  {
    path: 'request-durations',
    loadComponent: () =>
      import('./features/request-durations/request-durations').then((m) => m.RequestDurations),
  },
  {
    path: 'db-durations',
    loadComponent: () =>
      import('./features/db-durations/db-durations').then((m) => m.DbDurations),
  },
  {
    path: 'performance-counters',
    loadComponent: () =>
      import('./features/performance-counters/performance-counters').then((m) => m.PerformanceCounters),
  },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: '**', redirectTo: 'dashboard' },
];

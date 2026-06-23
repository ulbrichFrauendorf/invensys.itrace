import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard.page').then((m) => m.DashboardPage),
  },
  {
    path: 'applications',
    loadComponent: () =>
      import('./features/applications/applications.page').then((m) => m.ApplicationsPage),
  },
  {
    path: 'errors',
    loadComponent: () =>
      import('./features/errors/errors.page').then((m) => m.ErrorsPage),
  },
  {
    path: 'request-durations',
    loadComponent: () =>
      import('./features/request-durations/request-durations.page').then((m) => m.RequestDurationsPage),
  },
  {
    path: 'db-durations',
    loadComponent: () =>
      import('./features/db-durations/db-durations.page').then((m) => m.DbDurationsPage),
  },
  {
    path: 'performance-counters',
    loadComponent: () =>
      import('./features/performance-counters/performance-counters.page').then((m) => m.PerformanceCountersPage),
  },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: '**', redirectTo: 'dashboard' },
];

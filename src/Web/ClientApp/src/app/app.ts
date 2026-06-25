import { Component } from '@angular/core';
import { ITag, LayoutComponent, LayoutConfig, MenuModel } from 'invensys-ng';

@Component({
  selector: 'app-root',
  imports: [LayoutComponent, ITag],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly layoutConfig: LayoutConfig = {
    websiteName: 'iTrace',
    logoLight: 'assets/layout/images/invensys-icon-light.png',
    logoDark: 'assets/layout/images/invensys-icon-dark.png',
    showThemeToggle: true,
    enablePullToRefresh: true,
  };

  protected readonly menuModel: MenuModel[] = [
    {
      label: 'Observability',
      items: [
        { label: 'Dashboard', icon: 'pi pi-home', routerLink: ['/dashboard'] },
        { label: 'Errors', icon: 'pi pi-exclamation-triangle', routerLink: ['/errors'] },
        { label: 'Requests', icon: 'pi pi-clock', routerLink: ['/request-durations'] },
        { label: 'Database', icon: 'pi pi-database', routerLink: ['/db-durations'] },
        { label: 'Performance counters', icon: 'pi pi-server', routerLink: ['/performance-counters'] },
      ],
    },
    {
      label: 'Administration',
      items: [
        { label: 'Applications', icon: 'pi pi-key', routerLink: ['/applications'] },
      ],
    },
  ];
}

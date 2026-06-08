import { Component } from '@angular/core';
import { LayoutComponent, LayoutConfig, MenuModel } from 'integra-ng';

@Component({
  selector: 'app-root',
  imports: [LayoutComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly layoutConfig: LayoutConfig = {
    websiteName: 'iTrace',
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

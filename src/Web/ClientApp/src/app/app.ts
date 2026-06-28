import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ISelect, ITag, LayoutComponent, LayoutConfig, MenuModel } from 'invensys-ng';
import { ApplicationContextService } from './core/application-context.service';

@Component({
  selector: 'app-root',
  imports: [FormsModule, ISelect, LayoutComponent, ITag],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly applicationContext = inject(ApplicationContextService);

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

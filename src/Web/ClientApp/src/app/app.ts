import { Component, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { AuthAccessService } from './core/auth';
import { FormsModule } from '@angular/forms';
import {
  IButton,
  ISelect,
  ITag,
  IWhisper,
  LayoutComponent,
  LayoutConfig,
  MenuModel,
  TooltipDirective,
} from 'invensys-ng';
import { ApplicationContextService } from './core/application-context.service';

@Component({
  selector: 'app-shell',
  imports: [FormsModule, ISelect, LayoutComponent, ITag, IButton, TooltipDirective],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './app.scss',
})
export class AppShell {
  protected readonly applicationContext = inject(ApplicationContextService);
  protected readonly access = inject(AuthAccessService);
  protected readonly auth = inject(OidcSecurityService);

  protected readonly layoutConfig: LayoutConfig = {
    websiteName: 'iTrace',
    logoLight: 'assets/layout/images/invensys-icon-light.png',
    logoDark: 'assets/layout/images/invensys-icon-dark.png',
    showThemeToggle: true,
    enablePullToRefresh: true,
  };

  protected readonly menuModel = computed<MenuModel[]>(() => [
    {
      label: 'Observability',
      items: [
        { label: 'Dashboard', icon: 'pi pi-home', routerLink: ['/dashboard'] },
        { label: 'Errors', icon: 'pi pi-exclamation-triangle', routerLink: ['/errors'] },
        { label: 'Requests', icon: 'pi pi-clock', routerLink: ['/request-durations'] },
        { label: 'Database', icon: 'pi pi-database', routerLink: ['/db-durations'] },
        {
          label: 'Performance counters',
          icon: 'pi pi-server',
          routerLink: ['/performance-counters'],
        },
      ],
    },
    ...(this.access.access()?.canManageApplications
      ? [
          {
            label: 'Administration',
            items: [{ label: 'Applications', icon: 'pi pi-key', routerLink: ['/applications'] }],
          },
        ]
      : []),
  ]);
}

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, IWhisper],
  template: '<i-whisper /><router-outlet />',
})
export class App {}

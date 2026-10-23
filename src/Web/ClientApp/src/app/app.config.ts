import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { HttpClient, provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import {
  provideAuth,
  StsConfigLoader,
  withAppInitializerAuthCheck,
} from 'angular-auth-oidc-client';
import { authConfigLoader, authorizeInterceptor } from './core/auth';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withFetch(), withInterceptors([authorizeInterceptor])),
    provideAuth(
      { loader: { provide: StsConfigLoader, useFactory: authConfigLoader, deps: [HttpClient] } },
      withAppInitializerAuthCheck(),
    ),
    provideRouter(routes),
  ],
};

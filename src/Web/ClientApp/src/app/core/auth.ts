import { HttpClient, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { OidcSecurityService, StsConfigHttpLoader } from 'angular-auth-oidc-client';
import { catchError, map, of, switchMap, tap } from 'rxjs';

interface AuthSettings {
  authority: string;
  clientId: string;
  scope: string;
}
interface AuthAccess {
  canManageApplications: boolean;
}

export function authConfigLoader(http: HttpClient) {
  return new StsConfigHttpLoader(
    http.get<AuthSettings>('/api/auth/settings').pipe(
      map((settings) => ({
        authority: settings.authority,
        clientId: settings.clientId,
        scope: settings.scope,
        redirectUrl: `${window.location.origin}/callback`,
        postLogoutRedirectUri: window.location.origin,
        postLoginRoute: '/dashboard',
        unauthorizedRoute: '/forbidden',
        forbiddenRoute: '/forbidden',
        responseType: 'code',
        silentRenew: true,
        useRefreshToken: true,
      })),
    ),
  );
}

export const authorizeInterceptor: HttpInterceptorFn = (request, next) => {
  const url = new URL(request.url, window.location.origin);
  if (
    url.origin !== window.location.origin ||
    !url.pathname.startsWith('/api/') ||
    url.pathname === '/api/auth/settings'
  ) {
    return next(request);
  }
  return inject(OidcSecurityService)
    .getAccessToken()
    .pipe(
      switchMap((token) =>
        next(token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request),
      ),
    );
};

@Injectable({ providedIn: 'root' })
export class AuthAccessService {
  private readonly http = inject(HttpClient);
  readonly access = signal<AuthAccess | null>(null);

  load() {
    return this.http
      .get<AuthAccess>('/api/auth/access')
      .pipe(tap((access) => this.access.set(access)));
  }
}

export const claimsGuard: CanActivateFn = (route) => {
  const access = inject(AuthAccessService);
  const router = inject(Router);
  return access.load().pipe(
    map((result) =>
      !route.data['administrator'] || result.canManageApplications
        ? true
        : router.createUrlTree(['/forbidden']),
    ),
    catchError(() => {
      access.access.set(null);
      return of(router.createUrlTree(['/forbidden']));
    }),
  );
};

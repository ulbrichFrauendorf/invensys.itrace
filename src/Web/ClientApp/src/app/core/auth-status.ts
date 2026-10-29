import { afterNextRender, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { DialogService, IDialogActions } from 'invensys-ng';
import { finalize } from 'rxjs';

@Component({
  selector: 'app-access-denied-dialog',
  imports: [IDialogActions],
  template: `
    <section class="access-denied" aria-label="Access information">
      <div class="access-summary">
        <span class="access-icon" aria-hidden="true"><i class="pi pi-lock"></i></span>
        <div class="access-message">
          <h2>You don’t have access to iTrace yet</h2>
          <p>Your account is signed in, but it doesn’t have permission to open this app.</p>
        </div>
      </div>

      <div class="access-guidance">
        <h3>What to do next</h3>
        <p>
          Ask your administrator to grant you iTrace access, then sign in again. You can also sign
          out to use a different account.
        </p>
      </div>

      <details class="access-requirements">
        <summary>Access requirements for your administrator</summary>
        <p>
          Your iServe access token needs the <code>itrace.access</code> system claim and one of
          these roles: <code>itrace.user</code>, <code>itrace.administrator</code>, or
          <code>itrace.superuser</code>.
        </p>
        <p>Managing applications requires the administrator or superuser role.</p>
      </details>

      @if (actionError()) {
        <p class="action-error" role="alert">{{ actionError() }}</p>
      }
    </section>
    <fieldset slot="footer" class="access-actions" [disabled]="busy()" aria-label="Account actions">
      <i-dialog-actions
        cancelLabel="Sign out"
        submitLabel="Sign in again"
        [submitDisabled]="busy()"
        (cancelEvent)="signOut()"
        (submitEvent)="signIn()"
      />
    </fieldset>
  `,
  styleUrl: './auth-status.scss',
})
export class AccessDeniedDialog {
  private readonly auth = inject(OidcSecurityService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly busy = signal(false);
  protected readonly actionError = signal('');

  protected signIn(): void {
    this.actionError.set('');
    try {
      this.auth.authorize();
    } catch {
      this.actionError.set('Unable to open iServe sign-in. Please try again.');
    }
  }

  protected signOut(): void {
    this.busy.set(true);
    this.actionError.set('');
    this.auth
      .logoff()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.busy.set(false)),
      )
      .subscribe({
        error: () => this.actionError.set('Unable to sign out. Please try again.'),
      });
  }
}

@Component({
  selector: 'app-auth-status',
  template: '',
})
export class AuthStatus {
  private readonly dialogs = inject(DialogService);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    afterNextRender(() => {
      const dialog = this.dialogs.open(AccessDeniedDialog, {
        header: 'iTrace access required',
        width: 'min(34rem, calc(100vw - 2rem))',
        modal: true,
        closable: false,
        dismissableMask: false,
        contentStyle: { 'max-height': 'calc(100dvh - 8rem)', overflow: 'auto' },
      });
      this.destroyRef.onDestroy(() => dialog.close());
    });
  }
}

@Component({
  selector: 'app-auth-callback',
  template: '<p role="status">Completing sign-in…</p>',
})
export class AuthCallback {}

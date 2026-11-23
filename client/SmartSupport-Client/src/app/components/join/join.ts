import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { OrgService, apiErrorMessage } from '../../services/org';
import { hasValidSession } from '../../services/auth-session';

/**
 * Handles invite links (/join/:code).
 * Signed out: remembers the code and sends the user to sign in; sign-in brings them back here.
 * Signed in: joins the organization and opens the chat.
 */
@Component({
  selector: 'app-join',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="join-page">
      <div class="join-card">
        @if (error()) {
          <div class="icon error">!</div>
          <h2>Couldn't join</h2>
          <p>{{ error() }}</p>
          <a routerLink="/onboarding" class="btn">Back</a>
        } @else {
          <div class="spinner"></div>
          <h2>Joining your organization…</h2>
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; width: 100%; margin: auto; }
    .join-page { min-height: 100vh; min-height: 100dvh; display: grid; place-items: center; padding: 16px; box-sizing: border-box; }
    .join-card {
      width: 100%; max-width: 380px; text-align: center; color: #ececec;
      background: #1c1c1c; border: 1px solid #2e2e2e; border-radius: 18px; padding: 32px 24px;
    }
    h2 { margin: 16px 0 8px; font-size: 1.3rem; font-weight: 600; }
    p { margin: 0 0 20px; color: #a1a1aa; line-height: 1.5; }
    .spinner {
      width: 36px; height: 36px; margin: 0 auto; border-radius: 50%;
      border: 3px solid #2e2e2e; border-top-color: #10a37f; animation: spin 0.8s linear infinite;
    }
    .icon.error {
      width: 44px; height: 44px; margin: 0 auto; border-radius: 50%; display: grid; place-items: center;
      background: rgba(239, 68, 68, 0.15); color: #fca5a5; font-weight: 700; font-size: 1.3rem;
    }
    .btn {
      display: inline-block; padding: 10px 20px; border-radius: 10px; background: #10a37f;
      color: #fff; text-decoration: none; font-weight: 500;
    }
    @keyframes spin { to { transform: rotate(360deg); } }
  `]
})
export class JoinOrg implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private orgService = inject(OrgService);
  private toastr = inject(ToastrService);

  error = signal<string | null>(null);

  async ngOnInit() {
    const code = this.route.snapshot.paramMap.get('code') ?? '';

    if (!hasValidSession()) {
      OrgService.setPendingInvite(code);
      this.router.navigate(['/']);
      return;
    }

    try {
      const me = await this.orgService.loadMe();
      if (me.org) {
        OrgService.clearPendingInvite();
        this.toastr.info(`You're already a member of ${me.org.name}.`);
        this.router.navigate(['/chat']);
        return;
      }

      await this.orgService.joinOrg(code);
      OrgService.clearPendingInvite();
      this.toastr.success(`Welcome to ${this.orgService.org()?.name}!`);
      this.router.navigate(['/chat']);
    } catch (err) {
      OrgService.clearPendingInvite();
      this.error.set(apiErrorMessage(err, 'This invite link is not valid. Ask your admin for a new one.'));
    }
  }
}

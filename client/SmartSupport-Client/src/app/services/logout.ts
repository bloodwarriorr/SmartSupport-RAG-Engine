import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { ChatService } from './chat';
import { OrgService } from './org';
import { clearSession } from './auth-session';

declare const google: any;

/** Signs the user out everywhere in the app and returns to the landing page. */
@Injectable({ providedIn: 'root' })
export class LogoutService {
  private router = inject(Router);
  private authService = inject(SocialAuthService);
  private chatService = inject(ChatService);
  private orgService = inject(OrgService);

  async logout() {
    clearSession();
    OrgService.clearPendingInvite();
    this.chatService.reset();
    this.orgService.reset();

    // Stop Google from silently signing the same account back in.
    try { google?.accounts?.id?.disableAutoSelect(); } catch {}
    // signOut() rejects if the library has no user in memory (e.g. after a page reload); that's fine.
    try { await this.authService.signOut(); } catch {}

    this.router.navigate(['/']);
  }
}

import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { OrgService, apiErrorMessage } from '../../services/org';
import { LogoutService } from '../../services/logout';
import { getSessionUser } from '../../services/auth-session';

/** First stop after sign-in for users without an organization: create one or join with an invite code. */
@Component({
  selector: 'app-onboarding',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './onboarding.html',
  styleUrl: './onboarding.scss'
})
export class Onboarding {
  private orgService = inject(OrgService);
  private router = inject(Router);
  private toastr = inject(ToastrService);
  private logoutService = inject(LogoutService);

  user = getSessionUser();
  orgName = '';
  inviteCode = OrgService.getPendingInvite() ?? '';
  busy = signal<'create' | 'join' | null>(null);

  async create() {
    const name = this.orgName.trim();
    if (name.length < 2 || this.busy()) return;

    this.busy.set('create');
    try {
      await this.orgService.createOrg(name);
      this.toastr.success(`${name} is ready. Upload your first document to get started.`);
      this.router.navigate(['/chat']);
    } catch (err) {
      this.toastr.error(apiErrorMessage(err));
    } finally {
      this.busy.set(null);
    }
  }

  async join() {
    const code = this.inviteCode.trim();
    if (!code || this.busy()) return;

    this.busy.set('join');
    try {
      await this.orgService.joinOrg(code);
      OrgService.clearPendingInvite();
      this.toastr.success(`Welcome to ${this.orgService.org()?.name}!`);
      this.router.navigate(['/chat']);
    } catch (err) {
      this.toastr.error(apiErrorMessage(err));
    } finally {
      this.busy.set(null);
    }
  }

  logout() {
    this.logoutService.logout();
  }
}

import { inject } from '@angular/core';
import { CanActivateFn, Router, UrlTree } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { Me, OrgService } from '../services/org';
import { clearSession } from '../services/auth-session';

/**
 * Loads the user's organization, retrying once (e.g. while the API is restarting after a deploy).
 * Returns a redirect when the user can't continue: sign-in again on 401, or stay put on other errors.
 */
async function loadMeOrRedirect(): Promise<Me | UrlTree | false> {
  const router = inject(Router);
  const orgService = inject(OrgService);
  const toastr = inject(ToastrService);

  for (let attempt = 1; attempt <= 2; attempt++) {
    try {
      return await orgService.loadMe(true);
    } catch (err: any) {
      if (err?.status === 401) {
        clearSession();
        return router.parseUrl('/');
      }
      if (attempt === 1) await new Promise(r => setTimeout(r, 1500));
    }
  }

  // Never let the user into the chat without knowing their organization.
  toastr.error("Can't reach the server right now. Please refresh in a moment.");
  return false;
}

/** The chat needs an organization: users without one go to the create/join screen. */
export const orgGuard: CanActivateFn = async () => {
  const router = inject(Router);
  const result = await loadMeOrRedirect();
  if (result === false || result instanceof UrlTree) return result;
  return result.org ? true : router.parseUrl('/onboarding');
};

/** The create/join screen is only for users who don't have an organization yet. */
export const noOrgGuard: CanActivateFn = async () => {
  const router = inject(Router);
  const result = await loadMeOrRedirect();
  if (result === false || result instanceof UrlTree) return result;
  return result.org ? router.parseUrl('/chat') : true;
};

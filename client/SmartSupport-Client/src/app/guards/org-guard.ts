import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { OrgService } from '../services/org';

/** The chat needs an organization: users without one go to the create/join screen. */
export const orgGuard: CanActivateFn = async () => {
  const router = inject(Router);
  try {
    const me = await inject(OrgService).loadMe();
    return me.org ? true : router.parseUrl('/onboarding');
  } catch {
    // e.g. the token was rejected; let the chat page handle sign-out.
    return true;
  }
};

/** The create/join screen is only for users who don't have an organization yet. */
export const noOrgGuard: CanActivateFn = async () => {
  const router = inject(Router);
  try {
    const me = await inject(OrgService).loadMe();
    return me.org ? router.parseUrl('/chat') : true;
  } catch {
    return true;
  }
};

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { hasValidSession } from '../services/auth-session';
import { OrgService } from '../services/org';

/** Sends already signed-in users past the landing page (to a pending invite, or the chat). */
export const guestGuard: CanActivateFn = () => {
  if (!hasValidSession()) return true;

  const router = inject(Router);
  const invite = OrgService.getPendingInvite();
  return invite ? router.createUrlTree(['/join', invite]) : router.parseUrl('/chat');
};

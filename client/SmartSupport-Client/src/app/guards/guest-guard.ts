import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { hasValidSession } from '../services/auth-session';

/** Sends already signed-in users straight to the chat instead of the landing page. */
export const guestGuard: CanActivateFn = () => {
  return hasValidSession() ? inject(Router).parseUrl('/chat') : true;
};

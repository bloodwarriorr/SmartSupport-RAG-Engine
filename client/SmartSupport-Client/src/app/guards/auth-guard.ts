import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';

export const authGuard: CanActivateFn = () => {
  const router = inject(Router);
  const token = localStorage.getItem('idToken');

  if (token) {
    return true; 
  }

  return router.parseUrl('/login'); 
};
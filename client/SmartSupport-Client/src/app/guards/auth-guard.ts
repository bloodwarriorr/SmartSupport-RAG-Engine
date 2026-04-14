import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { map, take } from 'rxjs';

export const authGuard: CanActivateFn = () => {
  const authService = inject(SocialAuthService);
  const router = inject(Router);

  return authService.authState.pipe(
    take(1),
    map(user => {
      if (user) {
        return true; 
      } else {
        router.navigate(['/login']); 
        return false;
      }
    })
  );
};
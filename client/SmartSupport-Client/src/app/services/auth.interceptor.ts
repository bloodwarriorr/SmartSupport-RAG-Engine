import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { switchMap, take } from 'rxjs';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(SocialAuthService);

  return authService.authState.pipe(
    take(1),
    switchMap((user) => {
      
      if (user && user.idToken) {
        const cloned = req.clone({
          setHeaders: {
            Authorization: `Bearer ${user.idToken}`
          }
        });
        return next(cloned);
      }
     
      return next(req);
    })
  );
};
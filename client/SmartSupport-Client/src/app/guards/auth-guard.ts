import { inject } from '@angular/core';
import { Router, CanActivateFn } from '@angular/router';
import Swal from 'sweetalert2';

export const authGuard: CanActivateFn = async () => {
  const router = inject(Router);
  const token = localStorage.getItem('idToken');

  const handleExpiredSession = async (message: string) => {
    localStorage.removeItem('idToken');
    
    await Swal.fire({
      title: 'Session Expired',
      text: 'Your session has expired for security reasons. Please log in again to continue.',
      icon: 'info',
      confirmButtonText: 'OK',
      confirmButtonColor: '#3085d6',
      allowOutsideClick: false
    });

    return router.parseUrl('/login');
  };

  if (token) {
    try {
      const payloadBase64 = token.split('.')[1];
      const payloadJson = JSON.parse(atob(payloadBase64));

      const expiryTime = payloadJson.exp * 1000;
      const currentTime = Date.now();

      if (currentTime >= expiryTime) {
  
        return await handleExpiredSession('Expired');
      }

      return true; 
    } catch {
      localStorage.removeItem('idToken');
      return router.parseUrl('/login');
    }
  }

  
  return router.parseUrl('/login');
};
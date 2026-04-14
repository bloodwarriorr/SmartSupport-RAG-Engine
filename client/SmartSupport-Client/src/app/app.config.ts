import { ApplicationConfig, importProvidersFrom, InjectionToken } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { routes } from './app.routes';
import { 
  SocialLoginModule, 
  GoogleLoginProvider, 
  SocialAuthService 
} from '@abacritt/angularx-social-login';
import { environment } from '../environments/environment';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    provideHttpClient(),
    importProvidersFrom(SocialLoginModule),
    
    // פתרון ה-InjectionToken לגרסאות מתקדמות
    {
      provide: 'SocialAuthServiceConfig', // נשאר עבור הספרייה
      useValue: {
        autoLogin: false,
        providers: [
          {
            id: GoogleLoginProvider.PROVIDER_ID,
            provider: new GoogleLoginProvider(environment.googleClientId)
          }
        ],
        onError: (err: any) => console.error('Auth Error:', err)
      }
    },
    SocialAuthService
  ]
};
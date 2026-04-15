import { ApplicationConfig, importProvidersFrom } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http'; 
import { routes } from './app.routes';
import { 
  SocialLoginModule, 
  GoogleLoginProvider, 
  SocialAuthService 
} from '@abacritt/angularx-social-login';
import { environment } from '../environments/environment';
import { authInterceptor } from '../app/services/auth-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(routes),
    
    
    provideHttpClient(
      withInterceptors([authInterceptor])
    ),
    
    importProvidersFrom(SocialLoginModule),
    
    {
      provide: 'SocialAuthServiceConfig', 
      useValue: {
        autoLogin: false,
        providers: [
          {
            id: GoogleLoginProvider.PROVIDER_ID,
            provider: new GoogleLoginProvider(environment.googleClientId),
            oneTapEnabled: false,
            prompt: ''
          }
        ],
        onError: (err: any) => console.error('Auth Error:', err)
      }
    },
    SocialAuthService
  ]
};
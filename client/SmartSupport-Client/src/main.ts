import { bootstrapApplication } from '@angular/platform-browser';
import { AppComponent } from './app/app';
import { appConfig } from './app/app.config';

// All providers (router, HttpClient + auth interceptor, Google sign-in, toastr) live in app.config.ts.
bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));

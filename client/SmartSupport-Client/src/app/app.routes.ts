import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login';
import { Chat } from './components/chat/chat';
import { authGuard } from './guards/auth-guard';
import { guestGuard } from './guards/guest-guard';

export const routes: Routes = [
  { path: '', component: LoginComponent, canActivate: [guestGuard], pathMatch: 'full' },
  { path: 'login', redirectTo: '', pathMatch: 'full' },
  { path: 'chat', component: Chat, canActivate: [authGuard] },
  { path: '**', redirectTo: '' }
];

import { Routes } from '@angular/router';
import { LoginComponent } from './components/login/login';
import { Chat } from './components/chat/chat';
import { Onboarding } from './components/onboarding/onboarding';
import { JoinOrg } from './components/join/join';
import { Documents } from './components/documents/documents';
import { authGuard } from './guards/auth-guard';
import { guestGuard } from './guards/guest-guard';
import { orgGuard, noOrgGuard } from './guards/org-guard';

export const routes: Routes = [
  { path: '', component: LoginComponent, canActivate: [guestGuard], pathMatch: 'full' },
  { path: 'login', redirectTo: '', pathMatch: 'full' },
  // Invite links: /join/K7P2-XQ9M. Works before and after signing in.
  { path: 'join/:code', component: JoinOrg },
  { path: 'onboarding', component: Onboarding, canActivate: [authGuard, noOrgGuard] },
  { path: 'chat', component: Chat, canActivate: [authGuard, orgGuard] },
  { path: 'documents', component: Documents, canActivate: [authGuard, orgGuard] },
  { path: '**', redirectTo: '' }
];

import { Routes } from '@angular/router';
import { LoginComponent } from '../app/components/login/login';
import { Chat } from './components/chat/chat'; 
import { authGuard } from './guards/auth-guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { 
    path: 'chat', 
    component: Chat, 
    canActivate: [authGuard] 
  },
  { path: '', redirectTo: '/chat', pathMatch: 'full' },
  { path: '**', redirectTo: '/chat' }
];
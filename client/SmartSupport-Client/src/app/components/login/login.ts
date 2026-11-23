import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SocialAuthService, GoogleSigninButtonModule, SocialUser } from '@abacritt/angularx-social-login';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, GoogleSigninButtonModule],
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class LoginComponent implements OnInit, OnDestroy {
  private authService = inject(SocialAuthService);
  private router = inject(Router);
  private sub?: Subscription;

  mode: 'signin' | 'signup' = 'signin';
  year = new Date().getFullYear();

  features = [
    { icon: '📄', title: 'Your documents, your answers', text: 'Upload PDFs and SmartSupport answers only from what is inside them.' },
    { icon: '⚡', title: 'Instant, streamed replies', text: 'Answers appear word by word as they are generated.' },
    { icon: '🗂️', title: 'Chat history saved', text: 'Pick up any previous conversation right where you left it.' }
  ];

  ngOnInit() {
    this.sub = this.authService.authState.subscribe((user: SocialUser | null) => {
      if (user?.idToken) {
        localStorage.setItem('isLoggedIn', 'true');
        localStorage.setItem('idToken', user.idToken);
        this.router.navigate(['/chat']);
      }
    });
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }

  setMode(mode: 'signin' | 'signup') {
    this.mode = mode;
  }
}

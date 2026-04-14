import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common'; 
import { SocialAuthService, GoogleSigninButtonModule } from '@abacritt/angularx-social-login';
import { Router } from '@angular/router';

@Component({
  selector: 'app-login',
  standalone: true, 
  imports: [CommonModule, GoogleSigninButtonModule], 
  templateUrl: './login.html',
  styleUrls: ['./login.scss']
})
export class LoginComponent implements OnInit {
  constructor(
    private authService: SocialAuthService, 
    private router: Router
  ) {}

  ngOnInit() {
    this.authService.authState.subscribe((user: any) => {
      if (user) {
        console.log('User logged in:', user);
        this.router.navigate(['/chat']);
      }
    });
  }
}
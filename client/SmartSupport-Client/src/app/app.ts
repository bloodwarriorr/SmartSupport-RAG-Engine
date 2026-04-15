import { Component, OnInit } from '@angular/core';
import { RouterOutlet, Router } from '@angular/router'; 
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { NavigationStart } from '@angular/router'; 
import { filter } from 'rxjs';
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class AppComponent implements OnInit {
  title = 'SmartSupport-Client';

  constructor(
    private authService: SocialAuthService,
    private router: Router 
  ) { }

ngOnInit() {
  
  this.authService.authState.subscribe((user) => {
    if (user) {
      localStorage.setItem('isLoggedIn', 'true');
      localStorage.setItem('idToken', user.idToken);
    }
  });


}
}
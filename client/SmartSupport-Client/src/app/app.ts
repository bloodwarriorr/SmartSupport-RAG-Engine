import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ChatComponent } from '../app/components/chat/chat';
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [ChatComponent], 
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class AppComponent {
  title = 'SmartSupport-Client';
}
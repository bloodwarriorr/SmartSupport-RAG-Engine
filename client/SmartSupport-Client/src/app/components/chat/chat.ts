import { Component, inject, OnInit, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ChatService } from '../../services/chat';
import { Router } from '@angular/router'; 
import { ToastrService } from 'ngx-toastr';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import Swal from 'sweetalert2';
import { clearSession, getSessionUser } from '../../services/auth-session';

declare const google: any;

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [FormsModule, CommonModule],
  templateUrl: './chat.html',
  styleUrl: './chat.scss'
})
export class Chat implements OnInit, AfterViewChecked {
  chatService = inject(ChatService);
  router = inject(Router); 
  private toastr = inject(ToastrService);
  private authService = inject(SocialAuthService);
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef;

  user = getSessionUser();
  lastUserQuery: string = '';
  userQuery: string = '';
  isAdmin: boolean = true;
  ngOnInit() {

    const savedToken = localStorage.getItem('idToken');

    if (!savedToken) {
      console.warn('ChatComponent: No token found, redirecting to login');
      this.router.navigate(['/login']);
      return;
    }


    this.chatService.loadUserSessions()
      .then(() => {
        const existingSessions = this.chatService.sessions();
        
        if (existingSessions && existingSessions.length > 0) {
          
          const mostRecentSessionId = existingSessions[0].id;
          this.chatService.loadHistory(mostRecentSessionId);
        } else {
          
          this.chatService.createNewSession();
        }
      })
      .catch((err) => {
        console.error('ChatComponent: Error loading sessions', err);
        
        
        if (err.status === 401) {
          localStorage.clear();
          this.router.navigate(['/login']);
        } else {
         
          this.chatService.createNewSession();
        }
      });
  }


  ngAfterViewChecked() {
    this.scrollToBottom();
  }

  send() {
    if (this.userQuery.trim() && !this.chatService.isLoading()) {
      this.lastUserQuery = this.userQuery;
      this.chatService.askQuestionStream(this.userQuery);
      this.userQuery = ''; 
    }
  }

  async logout() {
    const result = await Swal.fire({
      title: 'Log out?',
      text: 'You will need to sign in with Google again to continue.',
      icon: 'question',
      showCancelButton: true,
      confirmButtonText: 'Log out',
      cancelButtonText: 'Cancel',
      confirmButtonColor: '#10a37f',
      cancelButtonColor: '#4d4d4d',
      background: '#212121',
      color: '#ececec'
    });
    if (!result.isConfirmed) return;

    clearSession();
    this.chatService.reset();

    // Stop Google from silently signing the same account back in.
    try { google?.accounts?.id?.disableAutoSelect(); } catch {}
    // signOut() rejects if the library has no user in memory (e.g. after a page reload); that's fine.
    try { await this.authService.signOut(); } catch {}

    this.router.navigate(['/']);
  }

  startNewChat() {
    this.lastUserQuery = '';
    this.chatService.createNewSession();
  }

  private scrollToBottom(): void {
    try {
      if (this.scrollContainer) {
        this.scrollContainer.nativeElement.scrollTop = this.scrollContainer.nativeElement.scrollHeight;
      }
    } catch (err) {}
  }

  async onFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      const success = await this.chatService.uploadFile(file);
      if (success) {
        this.toastr.success('File uploaded successfully!');
      } else {
        this.toastr.success('Failed to upload file.');
      }
    }
  }
}
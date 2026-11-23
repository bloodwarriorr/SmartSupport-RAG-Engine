import { Component, inject, OnInit, ViewChild, ElementRef, AfterViewChecked, computed } from '@angular/core';
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

  // Sidebar starts open on desktop and closed on phones.
  sidebarOpen = typeof window !== 'undefined' ? window.innerWidth > 900 : true;

  /** Sessions grouped into Today / Yesterday / Previous 7 days / Previous 30 days / Older. */
  groupedSessions = computed(() => {
    const startOfToday = new Date();
    startOfToday.setHours(0, 0, 0, 0);
    const day = 24 * 60 * 60 * 1000;
    const buckets = [
      { label: 'Today', from: startOfToday.getTime() },
      { label: 'Yesterday', from: startOfToday.getTime() - day },
      { label: 'Previous 7 days', from: startOfToday.getTime() - 7 * day },
      { label: 'Previous 30 days', from: startOfToday.getTime() - 30 * day },
      { label: 'Older', from: -Infinity }
    ];
    const groups = buckets.map(b => ({ label: b.label, sessions: [] as any[] }));

    for (const s of this.chatService.sessions()) {
      const time = this.parseServerDate(s.createdAt);
      const index = buckets.findIndex(b => time >= b.from);
      groups[index].sessions.push(s);
    }
    return groups.filter(g => g.sessions.length > 0);
  });

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
    this.closeSidebarOnMobile();
  }

  selectSession(sessionId: string) {
    if (this.chatService.isLoading()) return;
    if (sessionId !== this.chatService.currentSessionId()) {
      this.lastUserQuery = '';
      this.chatService.currentResponse.set('');
      this.chatService.history.set([]);
      this.chatService.loadHistory(sessionId);
    }
    this.closeSidebarOnMobile();
  }

  toggleSidebar() {
    this.sidebarOpen = !this.sidebarOpen;
  }

  private closeSidebarOnMobile() {
    if (window.innerWidth <= 900) this.sidebarOpen = false;
  }

  /** The API returns UTC dates, sometimes without a "Z"; treat those as UTC too. */
  private parseServerDate(value: string): number {
    if (!value) return 0;
    const hasZone = /[zZ]|[+-]\d{2}:?\d{2}$/.test(value);
    return new Date(hasZone ? value : value + 'Z').getTime();
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
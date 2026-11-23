import { Component, inject, OnInit, ViewChild, ElementRef, AfterViewChecked, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ChatService } from '../../services/chat';
import { Router } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import Swal from 'sweetalert2';
import { getSessionUser } from '../../services/auth-session';
import { OrgService, apiErrorMessage } from '../../services/org';
import { LogoutService } from '../../services/logout';

const swalTheme = {
  background: '#212121',
  color: '#ececec',
  confirmButtonColor: '#10a37f',
  cancelButtonColor: '#4d4d4d'
};

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
}

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
  private logoutService = inject(LogoutService);
  orgService = inject(OrgService);
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef;

  user = getSessionUser();
  lastUserQuery: string = '';
  userQuery: string = '';
  isUploading = false;

  // Only admins can upload documents and invite people. The server enforces this too.
  isAdmin = this.orgService.isAdmin;
  org = this.orgService.org;
  hasDocuments = computed(() => (this.org()?.documentCount ?? 0) > 0);

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

    await this.logoutService.logout();
  }

  /** Admin: shows the invite link with copy / new code buttons, plus the member list. */
  async openInvite() {
    let code: string;
    let members: Awaited<ReturnType<OrgService['getMembers']>> = [];
    try {
      [code, members] = await Promise.all([this.orgService.getInviteCode(), this.orgService.getMembers()]);
    } catch (err) {
      this.toastr.error(apiErrorMessage(err, 'Could not load the invite link.'));
      return;
    }

    const render = (c: string) => `
      <p class="invite-help">Anyone with this link can join <b>${escapeHtml(this.org()?.name ?? '')}</b> as a member.
      Share it with your team on WhatsApp, Slack or email.</p>
      <div class="invite-box">
        <input id="invite-link" readonly value="${escapeHtml(this.orgService.inviteLink(c))}">
      </div>
      <p class="invite-code">Or use the code <b>${escapeHtml(c)}</b></p>
      <div class="invite-members">
        <div class="invite-members-title">Members (${members.length})</div>
        ${members.map(m => `
          <div class="invite-member">
            <span dir="auto">${escapeHtml(m.name || m.email)}</span>
            <span class="role ${m.role === 'Admin' ? 'admin' : ''}">${m.role}</span>
          </div>`).join('')}
      </div>`;

    const result = await Swal.fire({
      ...swalTheme,
      title: 'Invite people',
      html: render(code),
      confirmButtonText: 'Copy link',
      showDenyButton: true,
      denyButtonText: 'New code',
      denyButtonColor: '#3f3f46',
      showCloseButton: true,
      customClass: { popup: 'invite-popup' }
    });

    if (result.isConfirmed) {
      try {
        await navigator.clipboard.writeText(this.orgService.inviteLink(code));
        this.toastr.success('Invite link copied');
      } catch {
        this.toastr.info(this.orgService.inviteLink(code), 'Copy this link');
      }
    } else if (result.isDenied) {
      const confirm = await Swal.fire({
        ...swalTheme,
        title: 'Create a new code?',
        text: 'The current link and code will stop working.',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Create new code'
      });
      if (confirm.isConfirmed) {
        try {
          await this.orgService.regenerateInviteCode();
          this.toastr.success('New invite code created');
        } catch (err) {
          this.toastr.error(apiErrorMessage(err));
        }
      }
      // Show the dialog again with the current code.
      this.openInvite();
    }
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
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // allow choosing the same file again
    if (!file || this.isUploading) return;

    this.isUploading = true;
    const progress = this.toastr.info(`Uploading ${file.name}…`, '', { disableTimeOut: true, tapToDismiss: false });
    try {
      const result = await this.chatService.uploadFile(file);
      if (result.ok) {
        this.toastr.success(`${file.name} is ready. Ask anything about it!`);
        await this.orgService.loadMe(true); // refresh the document count
      } else {
        this.toastr.error(result.message || 'Failed to upload file.');
      }
    } finally {
      this.toastr.clear(progress.toastId);
      this.isUploading = false;
    }
  }
}
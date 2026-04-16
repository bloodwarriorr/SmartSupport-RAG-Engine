import { Component, inject, OnInit, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ChatService } from '../../services/chat';
import { Router } from '@angular/router'; 
import { ToastrService } from 'ngx-toastr';
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
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef;

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
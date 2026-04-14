import { Component, inject, OnInit, ViewChild, ElementRef, AfterViewChecked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { ChatService } from '../../services/chat';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [FormsModule, CommonModule],
  templateUrl: './chat.html',
  styleUrl: './chat.scss'
})
export class ChatComponent implements OnInit, AfterViewChecked {
  chatService = inject(ChatService);
  
  @ViewChild('scrollContainer') private scrollContainer!: ElementRef;

  lastUserQuery: string = '';
  userQuery: string = '';
  isAdmin: boolean = true;

  ngOnInit() {
    // 1. טעינת כל הסשנים הקיימים של המשתמש מהשרת
    this.chatService.loadUserSessions().then(() => {
      const existingSessions = this.chatService.sessions();
      
      if (existingSessions && existingSessions.length > 0) {
        // 2. אם יש היסטוריה, נטען אוטומטית את הסשן האחרון (הכי חדש)
        const mostRecentSessionId = existingSessions[0].id;
        this.chatService.loadHistory(mostRecentSessionId);
      } else {
        // 3. אם זה משתמש חדש בלי שיחות, ניצור לו סשן (GUID) חדש
        this.chatService.createNewSession();
      }
    });
  }

  ngAfterViewChecked() {
    this.scrollToBottom();
  }

  send() {
    // וודא שיש סשן פעיל לפני השליחה (הסרביס מנהל את ה-currentSessionId)
    if (this.userQuery.trim() && !this.chatService.isLoading()) {
      this.lastUserQuery = this.userQuery;
      
      // שליחת השאלה (הסרביס כבר יודע להשתמש ב-currentSessionId() שהגדרנו)
      this.chatService.askQuestionStream(this.userQuery);
      
      this.userQuery = ''; 
    }
  }

  // פונקציה לממשק (למשל עבור כפתור "צ'אט חדש")
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
        alert('File uploaded successfully!');
      } else {
        alert('Failed to upload file.');
      }
    }
  }
}
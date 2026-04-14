import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../services/chat';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './chat.html',
  styleUrl: './chat.scss'
})
export class ChatComponent {
chatService = inject(ChatService);
lastUserQuery: string = '';
userQuery: string = '';
isAdmin: boolean = true;

  send() {
    if (this.userQuery.trim()) {
      this.lastUserQuery = this.userQuery;
      this.chatService.askQuestionStream(this.userQuery);
      this.userQuery = ''; 
    }
  }
  async onFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      console.log('Uploading:', file.name);
      const success = await this.chatService.uploadFile(file);
      if (success) {
        alert('File uploaded successfully!');
      } else {
        alert('Failed to upload file.');
      }
    }
  }
}
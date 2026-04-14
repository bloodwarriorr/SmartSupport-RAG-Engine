import { Injectable, signal, inject } from '@angular/core';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { firstValueFrom } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private apiUrl = 'http://localhost:7123/api/AI';
  private authService = inject(SocialAuthService);

  // סיגנלים לניהול מצב האפליקציה
  currentResponse = signal<string>('');
  isLoading = signal<boolean>(false);
  history = signal<any[]>([]);
  sessions = signal<any[]>([]); // רשימת כל השיחות מהשרת
  currentSessionId = signal<string | null>(null); // ה-ID הפעיל כרגע

  /**
   * יוצר מזהה סשן חדש (GUID) ומאפס את המסך
   */
  createNewSession(): string {
    const newId = crypto.randomUUID();
    this.currentSessionId.set(newId);
    this.history.set([]);
    this.currentResponse.set('');
    return newId;
  }

  /**
   * טוען את כל הסשנים הקודמים של המשתמש מה-API
   */
  async loadUserSessions() {
    try {
      const user = await firstValueFrom(this.authService.authState);
      if (!user) return;

      const response = await fetch(`${this.apiUrl}/sessions`, {
        headers: { 'Authorization': `Bearer ${user.idToken}` }
      });

      if (response.ok) {
        const data = await response.json();
        this.sessions.set(data);
      }
    } catch (error) {
      console.error('Error loading sessions:', error);
    }
  }

  /**
   * טוען היסטוריית הודעות עבור סשן ספציפי
   */
  async loadHistory(sessionId: string) {
    this.currentSessionId.set(sessionId);
    try {
      const user = await firstValueFrom(this.authService.authState);
      if (!user) return;

      const response = await fetch(`${this.apiUrl}/sessions/${sessionId}`, {
        headers: { 'Authorization': `Bearer ${user.idToken}` }
      });

      if (response.ok) {
        const data = await response.json();
        this.history.set(data);
      }
    } catch (error) {
      console.error('Load history error:', error);
    }
  }

  /**
   * שליחת שאלה וקבלת תשובה ב-Stream
   */
  async askQuestionStream(query: string) {
    // אם אין סשן פעיל, ניצור אחד לפני השליחה
    let sessionId = this.currentSessionId();
    if (!sessionId) {
      sessionId = this.createNewSession();
    }

    this.currentResponse.set('');
    this.isLoading.set(true);

    try {
      const user = await firstValueFrom(this.authService.authState);
      if (!user) throw new Error('User not authenticated');

      const url = `${this.apiUrl}/ask-stream?query=${encodeURIComponent(query)}&sessionId=${sessionId}`;

      const response = await fetch(url, {
        method: 'GET',
        headers: { 'Authorization': `Bearer ${user.idToken}` }
      });

      if (!response.body) throw new Error('No response body');
      
      const reader = response.body.getReader();
      const decoder = new TextDecoder();

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value, { stream: true });
        this.currentResponse.update(prev => prev + chunk);
      }

      // בסיום הסטרים - מרעננים את ההיסטוריה כדי לכלול את ההודעות החדשות שנשמרו ב-DB
      await this.loadHistory(sessionId);
      // מרעננים גם את רשימת הסשנים (למקרה שזה סשן חדש שנוצר עכשיו בשרת)
      await this.loadUserSessions();

    } catch (error) {
      console.error('Streaming error:', error);
      this.currentResponse.set('שגיאה בחיבור לשרת ה-AI');
    } finally {
      this.isLoading.set(false);
    }
  }

  /**
   * העלאת קובץ לשרת ה-Ingestion
   */
  async uploadFile(file: File) {
    const formData = new FormData();
    formData.append('file', file);

    try {
      const user = await firstValueFrom(this.authService.authState);
      const response = await fetch('http://localhost:7123/api/Ingestion/upload', {
        method: 'POST',
        headers: {
          'Authorization': user ? `Bearer ${user.idToken}` : ''
        },
        body: formData
      });
      return response.ok;
    } catch (error) {
      console.error('Upload error:', error);
      return false;
    }
  }
}
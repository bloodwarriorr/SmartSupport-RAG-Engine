import { Injectable, signal, inject } from '@angular/core';
import { SocialAuthService } from '@abacritt/angularx-social-login';

import { environment } from '../../environments/environment';
@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private apiUrl = `${environment.API_URL}/api/AI`;
  private authService = inject(SocialAuthService);


  currentResponse = signal<string>('');
  isLoading = signal<boolean>(false);
  history = signal<any[]>([]);
  sessions = signal<any[]>([]); 
  currentSessionId = signal<string | null>(null); 


  createNewSession(): string {
    const newId = crypto.randomUUID();
    this.currentSessionId.set(newId);
    this.history.set([]);
    this.currentResponse.set('');
    return newId;
  }


  async loadUserSessions() {
    console.log('ChatService: Starting to load sessions...');
    try {
      const response = await fetch(`${this.apiUrl}/sessions`, {
        headers: { 'Authorization': `Bearer ${this.idToken}` }
      });

      if (response.ok) {
        const data = await response.json();
        this.sessions.set(data);
      }
    } catch (error) {
      console.error('Error loading sessions:', error);
    }
  }


  async loadHistory(sessionId: string) {
    this.currentSessionId.set(sessionId);
    try {
      const response = await fetch(`${this.apiUrl}/sessions/${sessionId}`, {
        headers: { 'Authorization': `Bearer ${this.idToken}` }
      });

      if (response.ok) {
        const data = await response.json();
        this.history.set(data);
      }
    } catch (error) {
      console.error('Load history error:', error);
    }
  }

 
async askQuestionStream(query: string) {
  let sessionId = this.currentSessionId();
  if (!sessionId) {
    sessionId = this.createNewSession();
  }

  this.currentResponse.set('');
  this.isLoading.set(true);

  try {
    const url = `${this.apiUrl}/ask-stream?query=${encodeURIComponent(query)}&sessionId=${sessionId}`;

    const response = await fetch(url, {
      method: 'GET',
      headers: { 'Authorization': `Bearer ${this.idToken}` }
    });

    if (!response.body) throw new Error('No response body');

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let accumulatedBuffer = ''; // באפר שיחזיק שאריות טקסט

    while (true) {
      const { value, done } = await reader.read();
      if (done) break;

      // 1. הוספת הצ'אנק החדש לבאפר
      accumulatedBuffer += decoder.decode(value, { stream: true });

      // 2. ניקוי "שאריות" של מערך ה-JSON (התווים [ , ])
      // אנחנו מחפשים דפוס של מחרוזת בתוך גרשיים
      const jsonStringPattern = /"((?:[^"\\]|\\.)*)"/g;
      let match;

      while ((match = jsonStringPattern.exec(accumulatedBuffer)) !== null) {
        // match[1] מכיל את הטקסט הנקי ללא הגרשיים החיצוניים
        let cleanToken = match[1];

        // טיפול בתווים מיוחדים (כמו \n או \") שהגיעו כטקסט
        cleanToken = cleanToken
          .replace(/\\n/g, '\n')
          .replace(/\\"/g, '"')
          .replace(/\\u([\da-f]{4})/gi, (match, grp) => {
             return String.fromCharCode(parseInt(grp, 16));
          });

       
        this.currentResponse.update(prev => prev + cleanToken);
        
        
        accumulatedBuffer = accumulatedBuffer.slice(jsonStringPattern.lastIndex);
        jsonStringPattern.lastIndex = 0; 
      }
    }

    
    await this.loadHistory(sessionId);
    this.currentResponse.set(''); 
    await this.loadUserSessions();

  } catch (error) {
    console.error('Streaming error:', error);
    this.currentResponse.set('Failed to connect to AI service');
  } finally {
    this.isLoading.set(false);
  }
}

  
  async uploadFile(file: File) {
    const formData = new FormData();
    formData.append('file', file);

    try {

      const response = await fetch(`${environment.API_URL}/api/Ingestion/upload`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${this.idToken}`
        },
        body: formData
      });
      return response.ok;
    } catch (error) {
      console.error('Upload error:', error);
      return false;
    }
  }

  private get idToken(): string | null {
    return localStorage.getItem('idToken');
  }
}
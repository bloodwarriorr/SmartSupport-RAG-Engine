import { Injectable, signal, inject } from '@angular/core';
import { SocialAuthService } from '@abacritt/angularx-social-login';


@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private apiUrl = 'http://localhost:7123/api/AI';
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

    while (true) {
      const { value, done } = await reader.read();
      if (done) break;

      let chunk = decoder.decode(value, { stream: true });


      chunk = chunk.replace(/^"|"$/g, '') 
                   .replace(/\\n/g, '\n')
                   .replace(/\\"/g, '"');

      this.currentResponse.update(prev => prev + chunk);
    }


    await this.loadHistory(sessionId);
    
    
    this.currentResponse.set(''); 

   
    await this.loadUserSessions();

  } catch (error) {
    console.error('Streaming error:', error);
    this.currentResponse.set('שגיאה בחיבור לשרת ה-AI');
  } finally {
    this.isLoading.set(false);
  }
}

  
  async uploadFile(file: File) {
    const formData = new FormData();
    formData.append('file', file);

    try {

      const response = await fetch('http://localhost:7123/api/Ingestion/upload', {
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
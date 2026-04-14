import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ChatService {
  private apiUrl = 'http://localhost:7123/api/AI';


  currentResponse = signal<string>('');

  isLoading = signal<boolean>(false);

  async askQuestionStream(query: string) {
    this.currentResponse.set('');
    this.isLoading.set(true);

    try {
      const response = await fetch(`${this.apiUrl}/ask-stream?query=${encodeURIComponent(query)}`);
      if (!response.body) throw new Error('No response body');

      const reader = response.body.getReader();
      const decoder = new TextDecoder();

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;

        const chunk = decoder.decode(value, { stream: true });

        // לפעמים מגיעים כמה אובייקטים בשורות נפרדות ב-Chunk אחד
        const lines = chunk.split('\n');

        for (const line of lines) {
          if (!line.trim()) continue;

          try {
            const parsed = JSON.parse(line);

            
            if (Array.isArray(parsed)) {
              parsed.forEach(item => {
                const text = typeof item === 'object' ? item.response : item;
                if (text) this.currentResponse.update(prev => prev + text);
              });
            }
           
            else if (parsed && typeof parsed === 'object' && parsed.response) {
              this.currentResponse.update(prev => prev + parsed.response);
            }
           
            else if (typeof parsed === 'string') {
              this.currentResponse.update(prev => prev + parsed);
            }
          } catch (e) {
            
            this.currentResponse.update(prev => prev + line);
          }
        }
      }
    } catch (error) {
      console.error('Streaming error:', error);
      this.currentResponse.set('Error while trying to establish a connection');
    } finally {
      this.isLoading.set(false);
    }
  }
  async uploadFile(file: File) {
  const formData = new FormData();
  formData.append('file', file);
debugger;
  try {
    const response = await fetch('http://localhost:7123/api/Ingestion/upload', {
      method: 'POST',
      body: formData
    });
    return response.ok;
  } catch (error) {
    console.error('Upload error:', error);
    return false;
  }
}
}
export class Chat { }

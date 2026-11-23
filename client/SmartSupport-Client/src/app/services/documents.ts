import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpEvent } from '@angular/common/http';
import { Observable, firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export interface OrgDocument {
  id: string;
  fileName: string;
  contentType: string;
  createdAt: string;
  chunkCount: number;
}

export const ALLOWED_EXTENSIONS = ['.pdf', '.txt', '.md'];
export const MAX_UPLOAD_BYTES = 10 * 1024 * 1024;

/** The organization's knowledge base: list, upload (admins) and delete (admins). */
@Injectable({ providedIn: 'root' })
export class DocumentsService {
  private http = inject(HttpClient);

  list(): Promise<OrgDocument[]> {
    return firstValueFrom(this.http.get<OrgDocument[]>(`${environment.API_URL}/api/documents`));
  }

  delete(id: string): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${environment.API_URL}/api/documents/${id}`));
  }

  /** Emits upload-progress events, then the final response once the server has processed the file. */
  upload(file: File): Observable<HttpEvent<unknown>> {
    const form = new FormData();
    form.append('file', file);
    return this.http.post(`${environment.API_URL}/api/documents`, form, {
      reportProgress: true,
      observe: 'events'
    });
  }

  /** Returns an error message if the file can't be uploaded, or null if it's fine. */
  static validate(file: File): string | null {
    const ext = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(ext)) return `${file.name}: only PDF, TXT and MD files are supported.`;
    if (file.size > MAX_UPLOAD_BYTES) return `${file.name} is larger than 10 MB.`;
    if (file.size === 0) return `${file.name} is empty.`;
    return null;
  }
}

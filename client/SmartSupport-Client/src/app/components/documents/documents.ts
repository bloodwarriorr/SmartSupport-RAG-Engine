import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpEventType } from '@angular/common/http';
import { ToastrService } from 'ngx-toastr';
import Swal from 'sweetalert2';
import { DocumentsService, OrgDocument } from '../../services/documents';
import { OrgService, apiErrorMessage } from '../../services/org';

interface UploadItem {
  id: number;
  name: string;
  progress: number; // 0-100 while sending the file
  status: 'uploading' | 'processing' | 'done' | 'error';
  error?: string;
}

/** Knowledge base page: everyone sees the documents; admins can upload and delete. */
@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [DatePipe, RouterLink],
  templateUrl: './documents.html',
  styleUrl: './documents.scss'
})
export class Documents implements OnInit {
  private documentsService = inject(DocumentsService);
  private orgService = inject(OrgService);
  private toastr = inject(ToastrService);

  org = this.orgService.org;
  isAdmin = this.orgService.isAdmin;

  documents = signal<OrgDocument[]>([]);
  loading = signal(true);
  uploads = signal<UploadItem[]>([]);
  deletingId = signal<string | null>(null);
  dragOver = signal(false);

  private nextUploadId = 1;
  private queue: Promise<void> = Promise.resolve();

  ngOnInit() {
    this.refresh();
  }

  async refresh() {
    try {
      this.documents.set(await this.documentsService.list());
    } catch (err) {
      this.toastr.error(apiErrorMessage(err, 'Could not load documents.'));
    } finally {
      this.loading.set(false);
    }
  }

  // ---------- Upload ----------

  onFileInput(event: Event) {
    const input = event.target as HTMLInputElement;
    this.addFiles(Array.from(input.files ?? []));
    input.value = '';
  }

  onDragOver(event: DragEvent) {
    if (!this.isAdmin()) return;
    event.preventDefault();
    this.dragOver.set(true);
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.dragOver.set(false);
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.dragOver.set(false);
    if (!this.isAdmin()) return;
    this.addFiles(Array.from(event.dataTransfer?.files ?? []));
  }

  private addFiles(files: File[]) {
    for (const file of files) {
      const problem = DocumentsService.validate(file);
      if (problem) {
        this.toastr.error(problem);
        continue;
      }
      const item: UploadItem = { id: this.nextUploadId++, name: file.name, progress: 0, status: 'uploading' };
      this.uploads.update(list => [...list, item]);
      // One file at a time keeps us well inside the embedding API's rate limits.
      this.queue = this.queue.then(() => this.uploadOne(file, item.id));
    }
  }

  private uploadOne(file: File, itemId: number): Promise<void> {
    return new Promise(resolve => {
      this.documentsService.upload(file).subscribe({
        next: event => {
          if (event.type === HttpEventType.UploadProgress) {
            const progress = event.total ? Math.round((event.loaded / event.total) * 100) : 0;
            // Once the bytes are sent, the server still has to read, split and embed the file.
            this.updateUpload(itemId, { progress, status: progress >= 100 ? 'processing' : 'uploading' });
          } else if (event.type === HttpEventType.Response) {
            this.updateUpload(itemId, { progress: 100, status: 'done' });
            this.toastr.success(`${file.name} is ready.`);
            this.refresh();
            this.orgService.loadMe(true);
            setTimeout(() => this.removeUpload(itemId), 2500);
            resolve();
          }
        },
        error: err => {
          this.updateUpload(itemId, { status: 'error', error: apiErrorMessage(err, 'Upload failed.') });
          resolve();
        }
      });
    });
  }

  private updateUpload(id: number, changes: Partial<UploadItem>) {
    this.uploads.update(list => list.map(u => (u.id === id ? { ...u, ...changes } : u)));
  }

  removeUpload(id: number) {
    this.uploads.update(list => list.filter(u => u.id !== id));
  }

  // ---------- Delete ----------

  async remove(doc: OrgDocument) {
    const result = await Swal.fire({
      title: 'Delete this document?',
      html: `<b>${escapeHtml(doc.fileName)}</b> will be removed for everyone in your organization. This can't be undone.`,
      icon: 'warning',
      showCancelButton: true,
      confirmButtonText: 'Delete',
      cancelButtonText: 'Cancel',
      confirmButtonColor: '#dc2626',
      cancelButtonColor: '#4d4d4d',
      background: '#212121',
      color: '#ececec'
    });
    if (!result.isConfirmed) return;

    this.deletingId.set(doc.id);
    try {
      await this.documentsService.delete(doc.id);
      this.documents.update(list => list.filter(d => d.id !== doc.id));
      this.orgService.loadMe(true);
      this.toastr.success(`${doc.fileName} deleted.`);
    } catch (err) {
      this.toastr.error(apiErrorMessage(err, 'Could not delete the document.'));
    } finally {
      this.deletingId.set(null);
    }
  }

  // ---------- Display helpers ----------

  typeLabel(doc: OrgDocument): string {
    const ext = (doc.contentType || doc.fileName.split('.').pop() || '').toLowerCase();
    return ext ? ext.toUpperCase() : 'FILE';
  }

  typeIcon(doc: OrgDocument): string {
    const label = this.typeLabel(doc);
    return label === 'PDF' ? '📕' : label === 'MD' ? '📝' : '📄';
  }

  /** The API returns UTC dates, sometimes without a "Z"; treat those as UTC too. */
  asDate(value: string): Date {
    const hasZone = /[zZ]|[+-]\d{2}:?\d{2}$/.test(value);
    return new Date(hasZone ? value : value + 'Z');
  }
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
}

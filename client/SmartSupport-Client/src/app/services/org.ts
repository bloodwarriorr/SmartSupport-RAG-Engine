import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export type OrgRole = 'Admin' | 'Member';

export interface MyOrg {
  id: string;
  name: string;
  role: OrgRole;
  documentCount: number;
}

export interface Me {
  user: { name: string | null; email: string };
  org: MyOrg | null;
}

export interface OrgMember {
  name: string | null;
  email: string;
  role: OrgRole;
  joinedAt: string;
}

const PENDING_INVITE_KEY = 'pendingInvite';

/** The signed-in user's organization and role. Loaded once after sign-in and cached. */
@Injectable({ providedIn: 'root' })
export class OrgService {
  private http = inject(HttpClient);
  private apiUrl = `${environment.API_URL}/api/orgs`;
  private loading: Promise<Me> | null = null;

  me = signal<Me | null>(null);
  org = computed(() => this.me()?.org ?? null);
  isAdmin = computed(() => this.org()?.role === 'Admin');

  /** Loads (or returns the cached) user + organization. Pass force to re-fetch. */
  loadMe(force = false): Promise<Me> {
    const cached = this.me();
    if (cached && !force) return Promise.resolve(cached);
    if (!this.loading || force) {
      this.loading = firstValueFrom(this.http.get<Me>(`${this.apiUrl}/me`))
        .then(me => {
          this.me.set(me);
          return me;
        })
        .finally(() => (this.loading = null));
    }
    return this.loading;
  }

  async createOrg(name: string): Promise<void> {
    await firstValueFrom(this.http.post(this.apiUrl, { name }));
    await this.loadMe(true);
  }

  async joinOrg(code: string): Promise<void> {
    await firstValueFrom(this.http.post(`${this.apiUrl}/join`, { code }));
    await this.loadMe(true);
  }

  async getInviteCode(): Promise<string> {
    const res = await firstValueFrom(this.http.get<{ code: string }>(`${this.apiUrl}/invite`));
    return res.code;
  }

  async regenerateInviteCode(): Promise<string> {
    const res = await firstValueFrom(this.http.post<{ code: string }>(`${this.apiUrl}/invite/regenerate`, {}));
    return res.code;
  }

  getMembers(): Promise<OrgMember[]> {
    return firstValueFrom(this.http.get<OrgMember[]>(`${this.apiUrl}/members`));
  }

  inviteLink(code: string): string {
    return `${window.location.origin}/join/${code}`;
  }

  reset() {
    this.me.set(null);
    this.loading = null;
  }

  // An invite link opened before signing in is remembered here until sign-in finishes.
  static setPendingInvite(code: string) {
    try { localStorage.setItem(PENDING_INVITE_KEY, code); } catch {}
  }
  static getPendingInvite(): string | null {
    try { return localStorage.getItem(PENDING_INVITE_KEY); } catch { return null; }
  }
  static clearPendingInvite() {
    try { localStorage.removeItem(PENDING_INVITE_KEY); } catch {}
  }
}

/** Reads the server's { message } error text, falling back to a generic message. */
export function apiErrorMessage(err: any, fallback = 'Something went wrong. Please try again.'): string {
  return err?.error?.message || (typeof err?.error === 'string' ? err.error : '') || fallback;
}

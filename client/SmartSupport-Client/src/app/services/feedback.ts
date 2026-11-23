import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export type RatingFilter = 'negative' | 'positive' | 'all';

export interface FeedbackItem {
  id: number;
  feedback: 1 | -1;
  question: string | null;
  answer: string;
  sources: { fileName: string; snippet: string; score: number }[];
  timestamp: string;
  sessionTitle: string;
  user: string;
}

export interface FeedbackResponse {
  summary: { positive: number; negative: number };
  items: FeedbackItem[];
}

/** Admin only: rated answers in the organization. */
@Injectable({ providedIn: 'root' })
export class FeedbackService {
  private http = inject(HttpClient);

  list(rating: RatingFilter): Promise<FeedbackResponse> {
    return firstValueFrom(
      this.http.get<FeedbackResponse>(`${environment.API_URL}/api/feedback`, { params: { rating } })
    );
  }
}

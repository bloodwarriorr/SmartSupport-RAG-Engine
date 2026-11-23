import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ToastrService } from 'ngx-toastr';
import { FeedbackItem, FeedbackService, RatingFilter } from '../../services/feedback';
import { OrgService, apiErrorMessage } from '../../services/org';
import { MarkdownPipe } from '../../pipes/markdown.pipe';

/** Admin page: answers members rated, newest first. "Not helpful" shows where the documents fall short. */
@Component({
  selector: 'app-feedback',
  standalone: true,
  imports: [DatePipe, RouterLink, MarkdownPipe],
  templateUrl: './feedback.html',
  styleUrl: './feedback.scss'
})
export class Feedback implements OnInit {
  private feedbackService = inject(FeedbackService);
  private toastr = inject(ToastrService);
  org = inject(OrgService).org;

  filter = signal<RatingFilter>('negative');
  items = signal<FeedbackItem[]>([]);
  summary = signal({ positive: 0, negative: 0 });
  loading = signal(true);
  expanded = signal<Set<number>>(new Set());

  total = computed(() => this.summary().positive + this.summary().negative);
  satisfaction = computed(() => {
    const total = this.total();
    return total ? Math.round((this.summary().positive / total) * 100) : null;
  });

  readonly filters: { value: RatingFilter; label: string }[] = [
    { value: 'negative', label: '👎 Not helpful' },
    { value: 'positive', label: '👍 Helpful' },
    { value: 'all', label: 'All' }
  ];

  ngOnInit() {
    this.load();
  }

  setFilter(value: RatingFilter) {
    if (value === this.filter()) return;
    this.filter.set(value);
    this.load();
  }

  async load() {
    this.loading.set(true);
    try {
      const res = await this.feedbackService.list(this.filter());
      this.items.set(res.items);
      this.summary.set(res.summary);
    } catch (err) {
      this.toastr.error(apiErrorMessage(err, 'Could not load feedback.'));
    } finally {
      this.loading.set(false);
    }
  }

  isExpanded(id: number) {
    return this.expanded().has(id);
  }

  toggle(id: number) {
    this.expanded.update(set => {
      const next = new Set(set);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  /** The API returns UTC dates, sometimes without a "Z"; treat those as UTC too. */
  asDate(value: string): Date {
    const hasZone = /[zZ]|[+-]\d{2}:?\d{2}$/.test(value);
    return new Date(hasZone ? value : value + 'Z');
  }
}

import { Pipe, PipeTransform } from '@angular/core';
import { Marked } from 'marked';

// GitHub-flavored Markdown; single line breaks become <br> so chat answers keep their shape.
const marked = new Marked({ gfm: true, breaks: true });

/**
 * Turns an AI answer written in Markdown into HTML.
 * Use with [innerHTML]: Angular's built-in sanitizer removes scripts and other unsafe HTML.
 */
@Pipe({ name: 'markdown', standalone: true })
export class MarkdownPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value) return '';
    return marked.parse(value, { async: false }) as string;
  }
}

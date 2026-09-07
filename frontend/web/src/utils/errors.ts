import { ApiError } from '../services/api';

export function messageFrom(err: unknown): string {
  if (err instanceof ApiError) {
    const body = err.body as { detail?: string; title?: string } | undefined;
    if (body?.detail) return body.detail;
    if (body?.title) return body.title;
    if (typeof err.body === 'string') return err.body;
    return err.message;
  }
  if (err instanceof Error) return err.message;
  return 'Request failed';
}

import type { QuoteLineItem, Quotation } from '../types/domain';
import { quotations } from './mockData';

export function getQuotations(): Quotation[] {
  return quotations.map((quote) => ({ ...quote, items: [...quote.items] }));
}

export function calculateQuoteTotal(items: QuoteLineItem[]): number {
  return items.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0);
}

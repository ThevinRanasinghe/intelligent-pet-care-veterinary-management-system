import { useMemo, useState } from 'react';
import { ArrowLeft, Calculator, ChevronRight, FileText, Search, X } from 'lucide-react';
import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import { Card } from '../../components/ui/Card';
import { getQuotations, calculateQuoteTotal } from '../../services/billingService';
import type { Quotation, QuoteLineItem } from '../../types/domain';
import { formatDate, formatLkr } from '../../utils/format';

const statusTone: Record<Quotation['status'], 'neutral' | 'warning' | 'success' | 'danger' | 'info'> = {
  Draft: 'neutral', PendingApproval: 'warning', Approved: 'success', Rejected: 'danger', RevisionRequested: 'info', Finalised: 'success'
};

export function BillingPage() {
  const [quotes, setQuotes] = useState(getQuotations());
  const [selectedId, setSelectedId] = useState(quotes[0]?.id ?? '');
  const [query, setQuery] = useState('');
  const selected = quotes.find((quote) => quote.id === selectedId);
  const filtered = useMemo(() => quotes.filter((quote) => `${quote.id} ${quote.petName} ${quote.ownerName}`.toLowerCase().includes(query.toLowerCase())), [quotes, query]);
  const total = selected ? calculateQuoteTotal(selected.items) : 0;

  const updateItem = (itemId: string, key: keyof QuoteLineItem, value: string) => {
    setQuotes((current) => current.map((quote) => quote.id !== selectedId ? quote : { ...quote, items: quote.items.map((item) => item.id === itemId ? {...item, [key]: key === 'quantity' || key === 'unitPrice' ? Number(value) : value} : item) }));
  };

  const addItem = () => setQuotes((current) => current.map((quote) => quote.id !== selectedId ? quote : { ...quote, items: [...quote.items, { id: `li-${Date.now()}`, category: 'Other', description: 'New service item', quantity: 1, unitPrice: 0 }] }));
  const removeItem = (itemId: string) => setQuotes((current) => current.map((quote) => quote.id !== selectedId ? quote : { ...quote, items: quote.items.filter((item) => item.id !== itemId) }));

  return <div className="page-wrap">
    <div className="page-heading"><div><div className="eyebrow">Component · Billing</div><h2>Quotations & billing</h2><p>Create transparent veterinary quotations and keep approval state visible.</p></div><div className="billing-kpi"><Calculator size={16}/><strong>Auto total</strong><span>Line items calculate instantly</span></div></div>
    <div className="billing-layout">
      <Card className="quote-list-card"><div className="card-header"><div><div className="eyebrow">Quotation queue</div><h3>Active quotations</h3></div><Badge tone="info">{filtered.length} records</Badge></div><div className="search-input compact"><Search size={16}/><input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search quotation" /></div><div className="quote-list">{filtered.map((quote) => <button className={`quote-list-item ${quote.id === selectedId ? 'selected' : ''}`} key={quote.id} onClick={() => setSelectedId(quote.id)}><div><strong>{quote.id}</strong><span>{quote.petName} · {quote.ownerName}</span><small>{formatDate(quote.appointmentDate)} · {quote.appointmentTime}</small></div><div className="quote-list-right"><strong>{formatLkr(calculateQuoteTotal(quote.items))}</strong><Badge tone={statusTone[quote.status]}>{quote.status === 'PendingApproval' ? 'Pending approval' : quote.status}</Badge><ChevronRight size={16}/></div></button>)}</div></Card>
      <Card className="quote-editor">{selected ? <><div className="editor-top"><div><div className="eyebrow">{selected.id}</div><h3>{selected.petName} · {selected.ownerName}</h3><p>{selected.veterinarianName} · {formatDate(selected.appointmentDate)} at {selected.appointmentTime} · {selected.branch}</p></div><Badge tone={statusTone[selected.status]}>{selected.status}</Badge></div><div className="quote-meta"><div><span>Owner budget</span><strong>{formatLkr(selected.budget)}</strong></div><div><span>Current total</span><strong className={total > selected.budget ? 'danger-text' : ''}>{formatLkr(total)}</strong></div><div><span>Remaining</span><strong>{formatLkr(Math.max(selected.budget - total, 0))}</strong></div></div><div className="line-items"><div className="line-items-header"><h4>Quotation line items</h4><Button variant="secondary" onClick={addItem}>Add item</Button></div>{selected.items.map((item) => <div className="line-item" key={item.id}><select value={item.category} onChange={(e) => updateItem(item.id,'category',e.target.value)}><option>Consultation</option><option>Examination</option><option>Treatment</option><option>Medicine</option><option>Other</option></select><input value={item.description} onChange={(e) => updateItem(item.id,'description',e.target.value)} /><input type="number" min="1" value={item.quantity} onChange={(e) => updateItem(item.id,'quantity',e.target.value)} /><input type="number" min="0" value={item.unitPrice} onChange={(e) => updateItem(item.id,'unitPrice',e.target.value)} /><strong>{formatLkr(item.quantity * item.unitPrice)}</strong><button className="remove-line" onClick={() => removeItem(item.id)} aria-label={`Remove ${item.description}`}><X size={15}/></button></div>)}</div><div className="quote-total"><span>Total quotation</span><strong>{formatLkr(total)}</strong></div><div className="editor-footer"><span className="muted"><FileText size={15}/> Created {formatDate(selected.createdAt.slice(0,10))}</span><div><Button variant="secondary">Save draft</Button><Button disabled={selected.status !== 'Draft'}>Send for approval</Button></div></div></> : <div className="empty-state"><ArrowLeft size={30}/><h3>Select a quotation</h3></div>}</Card>
    </div>
  </div>;
}

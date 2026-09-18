import { Construction, LayoutDashboard } from 'lucide-react';
import { Card } from '../components/ui/Card';

export function PlaceholderPage({ title }: { title: string }) {
  return <div className="page-wrap"><div className="page-heading"><div><div className="eyebrow">Dashboard placeholder</div><h2>{title}</h2><p>This route is intentionally scaffolded for the other team member's component.</p></div></div><Card className="placeholder-card"><div className="placeholder-icon"><Construction size={34} /></div><h3>Dashboard only</h3><p>Shared navigation, layout, role context and page structure are ready. Business components will be implemented on their owning branch.</p><div className="placeholder-mini"><LayoutDashboard size={16} /> Ready for team integration</div></Card></div>;
}

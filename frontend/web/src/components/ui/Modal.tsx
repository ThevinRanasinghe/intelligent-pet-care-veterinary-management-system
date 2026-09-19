import type { ReactNode } from 'react';
import { X } from 'lucide-react';
import { Button } from './Button';

export function Modal({ title, children, onClose }: { title: string; children: ReactNode; onClose: () => void }) {
  return <div className="modal-backdrop" role="presentation">
    <div className="modal" role="dialog" aria-modal="true" aria-label={title}>
      <div className="modal-header"><div><div className="eyebrow">PetCare AI</div><h3>{title}</h3></div><Button variant="ghost" aria-label="Close" onClick={onClose} icon={<X size={18} />} /></div>
      {children}
    </div>
  </div>;
}

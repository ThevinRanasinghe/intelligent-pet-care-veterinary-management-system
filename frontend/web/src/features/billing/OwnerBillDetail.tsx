import { Badge } from '../../components/ui/Badge';
import { Button } from '../../components/ui/Button';
import type { Prescription, Quotation } from '../../types/domain';
import { formatDate, formatLkr } from '../../utils/format';

const sectionTitle: React.CSSProperties = {
  fontSize: '11px',
  fontWeight: 700,
  letterSpacing: '0.06em',
  color: '#777777',
  margin: '16px 0 6px',
};

const detailRow: React.CSSProperties = {
  display: 'flex',
  flexWrap: 'wrap',
  gap: '0.4rem 1.5rem',
  fontSize: '0.85rem',
};

const fieldLabel: React.CSSProperties = { color: '#777777', fontSize: '0.75rem', fontWeight: 600 };

const medicationTone: Record<string, 'warning' | 'success' | 'danger' | 'neutral'> = {
  Pending: 'warning',
  Issued: 'success',
  Unavailable: 'danger',
};

const formatTime = (t?: string | null) => (t ? t.slice(0, 5) : null);

function MedicationCard({ med, index }: { med: Prescription; index: number }) {
  const status = med.requestStatus ?? 'Pending';
  const medicineLabel = [med.medicineName, med.medicineStrength].filter(Boolean).join(' ')
    + (med.medicineDosageForm ? ` (${med.medicineDosageForm})` : '');
  return (
    <div className="info-strip" style={{ alignItems: 'flex-start', flexDirection: 'column', gap: '0.4rem' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', gap: '0.5rem', width: '100%', flexWrap: 'wrap', alignItems: 'center' }}>
        <strong>{index + 1}. {medicineLabel || 'Medicine'}</strong>
        <Badge tone={medicationTone[status] ?? 'warning'}>{status}</Badge>
      </div>
      <div style={detailRow}>
        <span><span style={fieldLabel}>Quantity:</span> {med.quantity ?? 1}</span>
        <span><span style={fieldLabel}>Dosage:</span> {med.dosage}</span>
        {med.frequency && <span><span style={fieldLabel}>Frequency:</span> {med.frequency}</span>}
        {med.route && <span><span style={fieldLabel}>Route:</span> {med.route}</span>}
        <span><span style={fieldLabel}>Duration:</span> {med.durationDays} day{med.durationDays === 1 ? '' : 's'}</span>
      </div>
      {med.instructions && <div style={{ fontSize: '0.85rem' }}><span style={fieldLabel}>Instructions:</span> {med.instructions}</div>}
      {status === 'Unavailable' && med.unavailableReason && (
        <div className="muted" style={{ fontSize: '0.78rem' }}>Unavailable: {med.unavailableReason}</div>
      )}
      {med.medicineUnitPrice != null && status === 'Issued' && (
        <div className="muted" style={{ fontSize: '0.78rem' }}>
          Billed {med.quantity ?? 1} × {formatLkr(med.medicineUnitPrice)} = {formatLkr((med.quantity ?? 1) * med.medicineUnitPrice)}
        </div>
      )}
    </div>
  );
}

/**
 * Complete owner-facing bill view: invoice + payment details, the linked
 * consultation (pet / appointment / clinic / veterinarian), the billed line
 * items, and the veterinarian's medication instructions for every medicine
 * prescribed under the examination.
 */
export function OwnerBillDetail({ bill, onClose }: { bill: Quotation; onClose?: () => void }) {
  const medications = bill.medications ?? [];
  const start = formatTime(bill.appointmentStartTime);
  const end = formatTime(bill.appointmentEndTime);
  const prescriber = medications[0]?.veterinarianName ?? bill.veterinarianName;
  const prescriptionDate = medications[0]?.createdAt?.slice(0, 10);

  return (
    <div>
      {/* Invoice */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', flexWrap: 'wrap' }}>
        <strong style={{ fontSize: '1.05rem' }}>{bill.invoiceNumber ?? 'Invoice'}</strong>
        <Badge tone={bill.paymentStatus === 'Paid' ? 'success' : 'warning'}>
          {bill.paymentStatus === 'Paid' ? 'Paid' : 'Pending payment'}
        </Badge>
        <Badge tone="neutral">{bill.status}</Badge>
      </div>

      {/* Consultation */}
      <div style={sectionTitle}>CONSULTATION</div>
      <div style={detailRow}>
        <span><span style={fieldLabel}>Pet:</span> {bill.petName ?? '—'}</span>
        {bill.ownerName && bill.ownerName !== '—' && <span><span style={fieldLabel}>Owner:</span> {bill.ownerName}</span>}
        <span><span style={fieldLabel}>Appointment:</span> {bill.appointmentDate ? formatDate(bill.appointmentDate) : '—'}{start ? ` · ${start}${end ? `–${end}` : ''}` : ''}</span>
        {bill.clinicName && <span><span style={fieldLabel}>Clinic:</span> {bill.clinicName}</span>}
        <span><span style={fieldLabel}>Veterinarian:</span> Dr. {bill.veterinarianName ?? '—'}</span>
        {bill.examinationDate && <span><span style={fieldLabel}>Examined:</span> {formatDate(bill.examinationDate.slice(0, 10))}</span>}
      </div>

      {/* Bill summary */}
      <div style={sectionTitle}>BILL SUMMARY</div>
      <div className="table-wrap">
        <table>
          <thead><tr><th>Item</th><th>Qty</th><th>Unit price</th><th>Subtotal</th></tr></thead>
          <tbody>
            {bill.items.map((item) => (
              <tr key={item.id}>
                <td>{item.description}</td>
                <td>{item.quantity}</td>
                <td>{formatLkr(item.unitPrice)}</td>
                <td>{formatLkr(item.quantity * item.unitPrice)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div style={{ display: 'flex', gap: '1.5rem', flexWrap: 'wrap', marginTop: '10px', fontSize: '0.85rem' }}>
        <span><span style={fieldLabel}>Veterinarian charge:</span> <strong>{formatLkr(bill.veterinarianChargeTotal ?? 0)}</strong></span>
        <span><span style={fieldLabel}>Medicines:</span> <strong>{formatLkr(bill.medicineTotal ?? 0)}</strong></span>
        <span><span style={fieldLabel}>Total:</span> <strong>{formatLkr(bill.total ?? 0)}</strong></span>
      </div>

      {/* Prescription & medication instructions */}
      {medications.length > 0 && (
        <>
          <div style={sectionTitle}>PRESCRIPTION &amp; MEDICATION INSTRUCTIONS</div>
          <div style={detailRow}>
            {prescriber && <span><span style={fieldLabel}>Prescribed by:</span> Dr. {prescriber}</span>}
            {prescriptionDate && <span><span style={fieldLabel}>Prescription date:</span> {formatDate(prescriptionDate)}</span>}
          </div>
          <div style={{ display: 'grid', gap: '0.5rem', marginTop: '8px' }}>
            {medications.map((med, index) => <MedicationCard key={med.id} med={med} index={index} />)}
          </div>
        </>
      )}

      {/* Payment */}
      <div style={sectionTitle}>PAYMENT</div>
      <div style={detailRow}>
        <span><span style={fieldLabel}>Status:</span> {bill.paymentStatus === 'Paid' ? 'Paid' : 'Pending'}</span>
        {bill.paidAt && <span><span style={fieldLabel}>Paid on:</span> {formatDate(bill.paidAt.slice(0, 10))}</span>}
      </div>

      {onClose && (
        <div className="modal-actions">
          <Button variant="secondary" onClick={onClose}>Close</Button>
        </div>
      )}
    </div>
  );
}

export default OwnerBillDetail;

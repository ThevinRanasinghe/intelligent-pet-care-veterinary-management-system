import { useEffect, useMemo, useState } from 'react';
import { PawPrint } from 'lucide-react';
import { petService, type Pet } from '../../services/api';
import { CareHistoryView, loadCareEntries, type CareEntry } from './CareHistoryView';

/**
 * Staff-facing medical history: read-only examination → diagnosis →
 * treatment → prescription chains across all registered pets.
 */
export function MedicalHistoryPage() {
  const [entries, setEntries] = useState<CareEntry[]>([]);
  const [pets, setPets] = useState<Pet[]>([]);
  const [petFilter, setPetFilter] = useState('All');
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;

    async function load() {
      try {
        setLoading(true);
        setMessage('');
        const allPets = await petService.getAllPets();
        if (!active) return;
        setPets(allPets);
        const careEntries = await loadCareEntries(allPets);
        if (active) setEntries(careEntries);
      } catch {
        if (active) {
          setEntries([]);
          setMessage('Could not load medical history. Please try again shortly.');
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    void load();
    return () => { active = false; };
  }, []);

  const visibleEntries = useMemo(
    () => (petFilter === 'All' ? entries : entries.filter((entry) => entry.pet.id === petFilter)),
    [entries, petFilter],
  );

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">CLINICAL RECORDS</span>
          <h2>Medical History</h2>
          <p>Examination, diagnosis, treatment, and prescription history across all patients.</p>
        </div>
        <div className="heading-actions">
          <select
            value={petFilter}
            onChange={(event) => setPetFilter(event.target.value)}
            aria-label="Filter by pet"
          >
            <option value="All">All patients</option>
            {pets.map((pet) => (
              <option key={pet.id} value={pet.id}>{pet.name} ({pet.species})</option>
            ))}
          </select>
        </div>
      </div>

      {loading ? (
        <div className="empty-state"><strong>Loading medical history…</strong></div>
      ) : message ? (
        <div className="empty-state"><PawPrint size={28} /><strong>{message}</strong></div>
      ) : (
        <CareHistoryView entries={visibleEntries} />
      )}
    </div>
  );
}

export default MedicalHistoryPage;

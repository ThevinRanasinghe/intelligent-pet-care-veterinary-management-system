import { useEffect, useState } from 'react';
import { PawPrint } from 'lucide-react';
import { useAuth } from '../auth/AuthContext';
import { ownerService, petService } from '../../services/api';
import { CareHistoryView, loadCareEntries, type CareEntry } from './CareHistoryView';

/** Read-only care information for the current pet owner's registered pets. */
export function OwnerCareHistoryPage() {
  const { user } = useAuth();
  const [entries, setEntries] = useState<CareEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState('');

  useEffect(() => {
    let active = true;

    async function loadCareHistory() {
      if (!user) return;

      try {
        setLoading(true);
        setMessage('');

        // Owner records are matched to the authenticated account email, then
        // every downstream request is limited to that owner's pet IDs.
        const owners = await ownerService.getAllOwners();
        const owner = owners.find((candidate) =>
          candidate.email.trim().toLowerCase() === user.email.trim().toLowerCase(),
        );

        if (!owner) {
          if (active) {
            setEntries([]);
            setMessage('Your account is not linked to a pet-owner profile yet. Add a pet from My Pets to get started.');
          }
          return;
        }

        const pets = await petService.getPetsByOwner(owner.id);
        const careEntries = await loadCareEntries(pets);

        if (active) setEntries(careEntries);
      } catch {
        if (active) {
          setEntries([]);
          setMessage('We could not load your pets’ care history. Please try again shortly.');
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    void loadCareHistory();
    return () => { active = false; };
  }, [user]);

  return (
    <div className="page-wrap">
      <div className="page-heading">
        <div>
          <span className="eyebrow">MY PETS’ HEALTH</span>
          <h2>Care History &amp; Prescriptions</h2>
          <p>Review consultation progress, treatments, and prescriptions for your registered pets.</p>
        </div>
      </div>

      {loading ? (
        <div className="empty-state"><strong>Loading care history…</strong></div>
      ) : message ? (
        <div className="empty-state"><PawPrint size={28} /><strong>{message}</strong></div>
      ) : (
        <CareHistoryView entries={entries} />
      )}
    </div>
  );
}

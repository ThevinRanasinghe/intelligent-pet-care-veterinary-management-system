import { useEffect, useMemo, useState } from 'react';
import { Check, Loader2 } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { Modal } from '../../components/ui/Modal';
import { useAuth } from '../auth/AuthContext';
import {
  consultationService,
  ownerService,
  petService,
  type Pet,
  type PetOwner,
  type WalkInBookingResponse,
} from '../../services/api';
import { getVeterinarians, type ManagerVeterinarian } from '../../services/managerService';
import { lookupsService, type OrganizationLookup } from '../../services/lookupsService';
import { messageFrom } from '../../utils/errors';

export interface WalkInPrefill {
  veterinarianId: string;
  /** YYYY-MM-DD */
  date: string;
  /** HH:mm */
  startTime: string;
}

interface WalkInBookingModalProps {
  /** Optional slot the manager clicked "Book" on — pre-fills vet/date/time. */
  prefill?: WalkInPrefill | null;
  onClose: () => void;
  onCreated: (result: WalkInBookingResponse) => void;
}

type PickMode = 'existing' | 'new';

const newOwnerEmpty = { fullName: '', email: '', phoneNumber: '', address: '' };
const newPetEmpty = { name: '', species: '', breed: '', gender: '', dateOfBirth: '', weight: '' };

/**
 * Front-desk walk-in intake. Instead of booking a bare appointment the form
 * files the same ConsultationRequest an online owner would — the backend
 * submits it and assigns the vet/slot through the shared workflow, so the
 * appointment stays linked (ConsultationRequestId) and the request shows up
 * in Consultation Requests with Source = WalkIn.
 */
export function WalkInBookingModal({ prefill, onClose, onCreated }: WalkInBookingModalProps) {
  const { user } = useAuth();

  const [owners, setOwners] = useState<PetOwner[]>([]);
  const [ownerQuery, setOwnerQuery] = useState('');
  const [ownerMode, setOwnerMode] = useState<PickMode>('existing');
  const [ownerId, setOwnerId] = useState('');
  const [newOwner, setNewOwner] = useState(newOwnerEmpty);

  const [pets, setPets] = useState<Pet[]>([]);
  const [petsLoading, setPetsLoading] = useState(false);
  const [petMode, setPetMode] = useState<PickMode>('existing');
  const [petId, setPetId] = useState('');
  const [newPet, setNewPet] = useState(newPetEmpty);

  const [organizations, setOrganizations] = useState<OrganizationLookup[]>([]);
  const [organizationId, setOrganizationId] = useState('');

  const [symptoms, setSymptoms] = useState('');
  const [urgency, setUrgency] = useState('Medium');
  const [budget, setBudget] = useState('');
  const [notes, setNotes] = useState('');

  const [vets, setVets] = useState<ManagerVeterinarian[]>([]);
  const [veterinarianId, setVeterinarianId] = useState(prefill?.veterinarianId ?? '');
  const [date, setDate] = useState(prefill?.date ?? '');
  const [startTime, setStartTime] = useState(prefill?.startTime ?? '');
  const [dayHours, setDayHours] = useState<string[] | null>(null);

  const [initializing, setInitializing] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState('');

  // ---- initial lookups ---------------------------------------------------
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const [ownerList, orgs, vetList] = await Promise.all([
          ownerService.getAllOwners(),
          lookupsService.getActiveOrganizations(),
          getVeterinarians(),
        ]);
        if (cancelled) return;
        setOwners(ownerList);
        setOrganizations(orgs);
        setVets(vetList.filter((v) => v.active));
        const mine = user?.organizationId;
        if (mine && orgs.some((o) => o.id === mine)) {
          setOrganizationId(mine);
        } else if (orgs.length === 1) {
          setOrganizationId(orgs[0].id);
        }
      } catch (err) {
        if (!cancelled) setFormError(messageFrom(err));
      } finally {
        if (!cancelled) setInitializing(false);
      }
    })();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ---- pets follow the selected owner ------------------------------------
  useEffect(() => {
    if (ownerMode !== 'existing' || !ownerId) {
      setPets([]);
      return;
    }
    let cancelled = false;
    setPetsLoading(true);
    petService.getPetsByOwner(ownerId)
      .then((list) => { if (!cancelled) setPets(list.filter((p) => !p.isArchived)); })
      .catch(() => { if (!cancelled) setPets([]); })
      .finally(() => { if (!cancelled) setPetsLoading(false); });
    return () => { cancelled = true; };
  }, [ownerMode, ownerId]);

  // ---- vet day availability drives the slot dropdown ----------------------
  useEffect(() => {
    if (!organizationId || !veterinarianId || !date) {
      setDayHours(null);
      return;
    }
    let cancelled = false;
    lookupsService.getAvailability(organizationId, date, veterinarianId)
      .then((day) => {
        if (cancelled) return;
        setDayHours(
          day.slots
            .filter((s) => s.available && s.availableVeterinarianIds.includes(veterinarianId))
            .map((s) => s.start.slice(0, 5)),
        );
      })
      .catch(() => { if (!cancelled) setDayHours([]); });
    return () => { cancelled = true; };
  }, [organizationId, veterinarianId, date]);

  const filteredOwners = useMemo(() => {
    const q = ownerQuery.trim().toLowerCase();
    if (!q) return owners;
    return owners.filter((o) =>
      `${o.fullName} ${o.email} ${o.phoneNumber}`.toLowerCase().includes(q));
  }, [owners, ownerQuery]);

  const switchOwner = (mode: PickMode) => {
    setOwnerMode(mode);
    setOwnerId('');
    setNewOwner(newOwnerEmpty);
    setPetMode(mode === 'new' ? 'new' : 'existing');
    setPetId('');
    setNewPet(newPetEmpty);
  };

  const switchPet = (mode: PickMode) => {
    setPetMode(mode);
    setPetId('');
    setNewPet(newPetEmpty);
  };

  const pickOwner = (id: string) => {
    if (id === ownerId) return;
    setOwnerId(id);
    setPetId('');
    setNewPet(newPetEmpty);
  };

  const validate = (): string | null => {
    if (ownerMode === 'existing' && !ownerId) return 'Select an existing owner or switch to registering a new one.';
    if (ownerMode === 'new') {
      if (!newOwner.fullName.trim()) return 'New owner: full name is required.';
      if (!newOwner.email.trim()) return 'New owner: email is required.';
      if (!newOwner.phoneNumber.trim()) return 'New owner: phone number is required.';
    }
    if (petMode === 'existing' && !petId) return 'Select a pet for this owner or switch to registering a new one.';
    if (petMode === 'new') {
      if (!newPet.name.trim()) return 'New pet: name is required.';
      if (!newPet.species.trim()) return 'New pet: species is required.';
      if (!newPet.breed.trim()) return 'New pet: breed is required.';
    }
    if (!organizationId) return 'Select the clinic.';
    if (!symptoms.trim()) return 'Describe the consultation reason / symptoms.';
    if (!veterinarianId) return 'Select a veterinarian.';
    if (!date) return 'Pick an appointment date.';
    if (!startTime) return 'Pick an available start time.';
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (submitting) return;
    const problem = validate();
    if (problem) { setFormError(problem); return; }
    setFormError('');
    setSubmitting(true);
    try {
      const result = await consultationService.createWalkInBooking({
        ownerId: ownerMode === 'existing' ? ownerId : null,
        newOwner: ownerMode === 'new'
          ? {
              fullName: newOwner.fullName.trim(),
              email: newOwner.email.trim(),
              phoneNumber: newOwner.phoneNumber.trim(),
              address: newOwner.address.trim() || null,
            }
          : null,
        petId: petMode === 'existing' ? petId : null,
        newPet: petMode === 'new'
          ? {
              name: newPet.name.trim(),
              species: newPet.species.trim(),
              breed: newPet.breed.trim() || null,
              gender: newPet.gender || null,
              dateOfBirth: newPet.dateOfBirth || null,
              weight: newPet.weight ? Number(newPet.weight) : null,
            }
          : null,
        symptoms: symptoms.trim(),
        urgency,
        budget: budget ? Number(budget) : null,
        additionalNotes: notes.trim() || null,
        organizationId,
        veterinarianId,
        date,
        // TimeOnly deserializes from "HH:mm:ss" — dropdown values are "HH:mm".
        startTime: startTime.length === 5 ? `${startTime}:00` : startTime,
      });
      onCreated(result);
    } catch (err) {
      setFormError(messageFrom(err));
      setSubmitting(false);
    }
  };

  const today = new Date().toISOString().slice(0, 10);

  return (
    <Modal title='Create walk-in booking' onClose={onClose}>
      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
        <p className='modal-copy'>
          Files a consultation request for the walk-in visitor and assigns the
          veterinarian/slot in one step — the booking follows the same
          workflow as an online request.
        </p>

        {initializing ? (
          <div className='empty-state'><Loader2 className='spinner' size={20} /> Loading owners, clinics and vets...</div>
        ) : (
          <>
            {/* ---------- Owner ---------- */}
            <div className='detail-block'>
              <span>Owner</span>
              <div className='scheduling-actions' style={{ margin: '6px 0' }}>
                <Button type='button' variant={ownerMode === 'existing' ? 'primary' : 'secondary'} onClick={() => switchOwner('existing')}>
                  Existing owner
                </Button>
                <Button type='button' variant={ownerMode === 'new' ? 'primary' : 'secondary'} onClick={() => switchOwner('new')}>
                  Register new owner
                </Button>
              </div>
              {ownerMode === 'existing' ? (
                <>
                  <input
                    value={ownerQuery}
                    onChange={(e) => setOwnerQuery(e.target.value)}
                    placeholder='Search name, email or phone'
                  />
                  <select
                    aria-label='Existing owner'
                    value={ownerId}
                    onChange={(e) => pickOwner(e.target.value)}
                    style={{ marginTop: '8px' }}
                  >
                    <option value=''>Select owner...</option>
                    {filteredOwners.map((o) => (
                      <option key={o.id} value={o.id}>{o.fullName} — {o.email}</option>
                    ))}
                  </select>
                </>
              ) : (
                <div className='form-grid' style={{ marginTop: '8px' }}>
                  <label>
                    Full name *
                    <input value={newOwner.fullName} onChange={(e) => setNewOwner({ ...newOwner, fullName: e.target.value })} />
                  </label>
                  <label>
                    Email *
                    <input type='email' value={newOwner.email} onChange={(e) => setNewOwner({ ...newOwner, email: e.target.value })} />
                  </label>
                  <label>
                    Phone *
                    <input value={newOwner.phoneNumber} onChange={(e) => setNewOwner({ ...newOwner, phoneNumber: e.target.value })} />
                  </label>
                  <label>
                    Address
                    <input value={newOwner.address} onChange={(e) => setNewOwner({ ...newOwner, address: e.target.value })} />
                  </label>
                </div>
              )}
            </div>

            {/* ---------- Pet ---------- */}
            <div className='detail-block'>
              <span>Pet</span>
              {ownerMode === 'new' ? (
                <p className='muted-line'>A new owner needs a new pet registration.</p>
              ) : (
                <div className='scheduling-actions' style={{ margin: '6px 0' }}>
                  <Button type='button' variant={petMode === 'existing' ? 'primary' : 'secondary'} onClick={() => switchPet('existing')} disabled={!ownerId}>
                    Existing pet
                  </Button>
                  <Button type='button' variant={petMode === 'new' ? 'primary' : 'secondary'} onClick={() => switchPet('new')} disabled={!ownerId}>
                    Register new pet
                  </Button>
                </div>
              )}
              {petMode === 'existing' ? (
                ownerId ? (
                  petsLoading ? (
                    <p className='muted-line'>Loading pets...</p>
                  ) : (
                    <select aria-label='Existing pet' value={petId} onChange={(e) => setPetId(e.target.value)}>
                      <option value=''>Select pet...</option>
                      {pets.map((p) => (
                        <option key={p.id} value={p.id}>{p.name} — {p.species}</option>
                      ))}
                    </select>
                  )
                ) : (
                  <p className='muted-line'>Select an owner first.</p>
                )
              ) : (
                <div className='form-grid' style={{ marginTop: '8px' }}>
                  <label>
                    Pet name *
                    <input value={newPet.name} onChange={(e) => setNewPet({ ...newPet, name: e.target.value })} />
                  </label>
                  <label>
                    Species *
                    <input value={newPet.species} onChange={(e) => setNewPet({ ...newPet, species: e.target.value })} placeholder='Dog, Cat...' />
                  </label>
                  <label>
                    Breed *
                    <input value={newPet.breed} onChange={(e) => setNewPet({ ...newPet, breed: e.target.value })} />
                  </label>
                  <label>
                    Gender
                    <select value={newPet.gender} onChange={(e) => setNewPet({ ...newPet, gender: e.target.value })}>
                      <option value=''>—</option>
                      <option value='Male'>Male</option>
                      <option value='Female'>Female</option>
                    </select>
                  </label>
                  <label>
                    Date of birth
                    <input type='date' value={newPet.dateOfBirth} onChange={(e) => setNewPet({ ...newPet, dateOfBirth: e.target.value })} />
                  </label>
                  <label>
                    Weight (kg)
                    <input type='number' min='0' step='0.1' value={newPet.weight} onChange={(e) => setNewPet({ ...newPet, weight: e.target.value })} />
                  </label>
                </div>
              )}
            </div>

            {/* ---------- Clinic + consultation ---------- */}
            <div className='form-grid'>
              <label>
                Clinic *
                <select value={organizationId} onChange={(e) => setOrganizationId(e.target.value)}>
                  <option value=''>Select clinic...</option>
                  {organizations.map((o) => (
                    <option key={o.id} value={o.id}>{o.name}{o.city ? ` — ${o.city}` : ''}</option>
                  ))}
                </select>
              </label>
              <label>
                Urgency
                <select value={urgency} onChange={(e) => setUrgency(e.target.value)}>
                  <option value='Low'>Low</option>
                  <option value='Medium'>Medium</option>
                  <option value='High'>High</option>
                  <option value='Emergency'>Emergency</option>
                </select>
              </label>
            </div>
            <label>
              Symptoms / reason *
              <textarea value={symptoms} onChange={(e) => setSymptoms(e.target.value)} rows={3} placeholder='Describe what the pet came in for' />
            </label>
            <div className='form-grid'>
              <label>
                Budget (LKR)
                <input type='number' min='0' value={budget} onChange={(e) => setBudget(e.target.value)} />
              </label>
              <label>
                Additional notes
                <input value={notes} onChange={(e) => setNotes(e.target.value)} />
              </label>
            </div>

            {/* ---------- Veterinarian + slot ---------- */}
            <div className='form-grid'>
              <label>
                Veterinarian *
                <select value={veterinarianId} onChange={(e) => { setVeterinarianId(e.target.value); setStartTime(''); }}>
                  <option value=''>Select veterinarian...</option>
                  {vets.map((v) => (
                    <option key={v.id} value={v.id}>{v.name} — {v.specialisation}</option>
                  ))}
                </select>
              </label>
              <label>
                Date *
                <input type='date' min={today} value={date} onChange={(e) => { setDate(e.target.value); setStartTime(''); }} />
              </label>
            </div>
            <label>
              Start time *
              {dayHours === null ? (
                <select disabled>
                  <option>{veterinarianId && date ? 'Loading availability...' : 'Select vet, clinic and date first'}</option>
                </select>
              ) : dayHours.length === 0 ? (
                <>
                  <select disabled><option>No free slot for this vet on this date</option></select>
                  <p className='muted-line'>Pick another veterinarian or date.</p>
                </>
              ) : (
                <select value={startTime} onChange={(e) => setStartTime(e.target.value)}>
                  <option value=''>Select start time...</option>
                  {dayHours.map((h) => (
                    <option key={h} value={h}>{h} – {String(Number(h.slice(0, 2)) + 1).padStart(2, '0')}:{h.slice(3)}</option>
                  ))}
                </select>
              )}
            </label>
            {prefill && (
              <p className='muted-line'>
                Prefilled from the selected open slot — adjust vet/date/time if needed.
              </p>
            )}
          </>
        )}

        {formError && <div className='form-error'>{formError}</div>}
        <div className='modal-actions'>
          <Button type='button' variant='secondary' onClick={onClose}>Cancel</Button>
          <Button type='submit' disabled={initializing || submitting} icon={<Check size={16} />}>
            {submitting ? 'Creating booking...' : 'Create walk-in booking'}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

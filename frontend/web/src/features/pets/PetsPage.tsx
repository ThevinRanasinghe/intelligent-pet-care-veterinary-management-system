import { useEffect, useMemo, useState } from "react";
import { ownerService, petService, Pet, PetOwner } from "../../services/api";
import { UserPlus, Plus, PawPrint, Search, SlidersHorizontal, X } from "lucide-react";
import { useAuth } from "../auth/AuthContext";

type PetForm = {
  ownerId: string;
  name: string;
  species: string;
  breed: string;
  gender: string;
  dateOfBirth: string;
  weight: string;
};

type OwnerForm = {
  fullName: string;
  email: string;
  phoneNumber: string;
  address: string;
};

const emptyPetForm: PetForm = {
  ownerId: "",
  name: "",
  species: "",
  breed: "",
  gender: "",
  dateOfBirth: "",
  weight: "",
};

const emptyOwnerForm: OwnerForm = {
  fullName: "",
  email: "",
  phoneNumber: "",
  address: "",
};

function formatDate(value?: string | null) {
  if (!value) return "—";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleDateString();
}

export default function PetsPage() {
  const { user } = useAuth();
  const isPetOwner = user?.role === "PetOwner";
  const [pets, setPets] = useState<Pet[]>([]);
  const [owners, setOwners] = useState<PetOwner[]>([]);
  const [search, setSearch] = useState("");
  const [speciesFilter, setSpeciesFilter] = useState("All");

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");

  // Modals
  const [isPetModalOpen, setIsPetModalOpen] = useState(false);
  const [isOwnerModalOpen, setIsOwnerModalOpen] = useState(false);

  const [editingPet, setEditingPet] = useState<Pet | null>(null);

  const [petForm, setPetForm] = useState<PetForm>(emptyPetForm);
  const [ownerForm, setOwnerForm] = useState<OwnerForm>(emptyOwnerForm);

  // ------------------------------------------------------------
  // Load data
  // ------------------------------------------------------------

  async function loadData() {
    try {
      setLoading(true);
      setError("");

      const ownersData = await ownerService.getAllOwners();
      const currentOwner = isPetOwner
        ? ownersData.find((owner) => owner.email.trim().toLowerCase() === user?.email.trim().toLowerCase())
        : undefined;
      const petsData = isPetOwner
        ? currentOwner ? await petService.getPetsByOwner(currentOwner.id) : []
        : await petService.getAllPets();

      setPets(Array.isArray(petsData) ? petsData : []);
      setOwners(isPetOwner ? (currentOwner ? [currentOwner] : []) : ownersData);

      if (isPetOwner && !currentOwner) {
        setError("Your account is not linked to a pet-owner profile yet.");
      }
    } catch (err) {
      console.error(err);
      setError("Unable to load pets or owners. Make sure the ASP.NET Core backend is running.");
      setPets([]);
      setOwners([]);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  // Map owner names for lookup
  const ownerMap = useMemo(() => {
    const map = new Map<string, PetOwner>();
    owners.forEach((owner) => map.set(owner.id, owner));
    return map;
  }, [owners]);

  // ------------------------------------------------------------
  // Species list
  // ------------------------------------------------------------

  const speciesList = useMemo(() => {
    const values = pets
      .map((pet) => pet.species)
      .filter(Boolean)
      .map((species) => species.trim());

    return ["All", ...Array.from(new Set(values))];
  }, [pets]);

  // ------------------------------------------------------------
  // Search + filter
  // ------------------------------------------------------------

  const filteredPets = useMemo(() => {
    const searchValue = search.trim().toLowerCase();

    return pets.filter((pet) => {
      const owner = ownerMap.get(pet.ownerId);
      const ownerName = owner?.fullName ?? "";

      const matchesSearch =
        !searchValue ||
        pet.name.toLowerCase().includes(searchValue) ||
        pet.species.toLowerCase().includes(searchValue) ||
        (pet.breed ?? "").toLowerCase().includes(searchValue) ||
        pet.ownerId.toLowerCase().includes(searchValue) ||
        ownerName.toLowerCase().includes(searchValue);

      const matchesSpecies =
        speciesFilter === "All" || pet.species === speciesFilter;

      return matchesSearch && matchesSpecies;
    });
  }, [pets, search, speciesFilter, ownerMap]);

  // ------------------------------------------------------------
  // Pet Modal Actions
  // ------------------------------------------------------------

  function openAddPetModal(defaultOwnerId?: string) {
    setEditingPet(null);
    setPetForm({
      ...emptyPetForm,
      ownerId: defaultOwnerId || (owners.length > 0 ? owners[0].id : ""),
    });
    setError("");
    setSuccess("");
    setIsPetModalOpen(true);
  }

  function openEditPetModal(pet: Pet) {
    setEditingPet(pet);
    setPetForm({
      ownerId: pet.ownerId,
      name: pet.name,
      species: pet.species,
      breed: pet.breed ?? "",
      gender: pet.gender ?? "",
      dateOfBirth: pet.dateOfBirth ? pet.dateOfBirth.substring(0, 10) : "",
      weight:
        pet.weight !== null && pet.weight !== undefined
          ? String(pet.weight)
          : "",
    });
    setError("");
    setSuccess("");
    setIsPetModalOpen(true);
  }

  function closePetModal() {
    if (saving) return;
    setIsPetModalOpen(false);
    setEditingPet(null);
    setPetForm(emptyPetForm);
  }

  // ------------------------------------------------------------
  // Owner Modal Actions
  // ------------------------------------------------------------

  function openAddOwnerModal() {
    setOwnerForm(emptyOwnerForm);
    setError("");
    setSuccess("");
    setIsOwnerModalOpen(true);
  }

  function closeOwnerModal() {
    if (saving) return;
    setIsOwnerModalOpen(false);
    setOwnerForm(emptyOwnerForm);
  }

  // ------------------------------------------------------------
  // Create Owner Submit
  // ------------------------------------------------------------

  async function handleOwnerSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError("");
    setSuccess("");

    if (!ownerForm.fullName.trim()) {
      setError("Full Name is required.");
      return;
    }
    if (!ownerForm.email.trim()) {
      setError("Email is required.");
      return;
    }
    if (!ownerForm.phoneNumber.trim()) {
      setError("Phone Number is required.");
      return;
    }

    try {
      setSaving(true);
      const newOwner = await ownerService.createOwner({
        fullName: ownerForm.fullName.trim(),
        email: ownerForm.email.trim(),
        phoneNumber: ownerForm.phoneNumber.trim(),
        address: ownerForm.address.trim() || null,
      });

      setSuccess(`Owner registered successfully. Owner ID: ${newOwner.id}`);
      setIsOwnerModalOpen(false);
      setOwnerForm(emptyOwnerForm);

      // Refresh owners and prompt for pet registration
      const updatedOwners = await ownerService.getAllOwners();
      setOwners(updatedOwners);

      // Open pet modal with new owner selected
      openAddPetModal(newOwner.id);
    } catch (err: any) {
      console.error(err);
      setError(err?.message || "Unable to register owner.");
    } finally {
      setSaving(false);
    }
  }

  // ------------------------------------------------------------
  // Create / Edit Pet Submit
  // ------------------------------------------------------------

  async function handlePetSubmit(event: React.FormEvent) {
    event.preventDefault();
    setError("");
    setSuccess("");

    const ownerId = isPetOwner ? owners[0]?.id ?? "" : petForm.ownerId.trim();
    if (!ownerId) {
      setError(isPetOwner ? "Your account is not linked to a pet-owner profile yet." : "Please select or enter an Owner.");
      return;
    }

    if (!petForm.name.trim()) {
      setError("Pet name is required.");
      return;
    }

    if (!petForm.species.trim()) {
      setError("Species is required.");
      return;
    }

    let weight: number | null = null;
    if (petForm.weight.trim()) {
      weight = Number(petForm.weight);
      if (Number.isNaN(weight) || weight <= 0) {
        setError("Weight must be a valid positive number.");
        return;
      }
    }

    const payload = {
      ownerId,
      name: petForm.name.trim(),
      species: petForm.species.trim(),
      breed: petForm.breed.trim() || null,
      gender: petForm.gender || null,
      dateOfBirth: petForm.dateOfBirth || null,
      weight,
    };

    try {
      setSaving(true);

      if (editingPet) {
        await petService.updatePet(editingPet.id, payload);
        setSuccess("Pet details updated successfully.");
      } else {
        const createdPet = await petService.createPet(payload);
        setSuccess(`Pet registered successfully. Pet ID: ${createdPet.id}`);
      }

      await loadData();
      setIsPetModalOpen(false);
      setEditingPet(null);
      setPetForm(emptyPetForm);
    } catch (err: any) {
      console.error(err);
      setError(
        err?.message ||
          (editingPet ? "Unable to update the pet." : "Unable to register the pet.")
      );
    } finally {
      setSaving(false);
    }
  }

  // ------------------------------------------------------------
  // Delete Pet
  // ------------------------------------------------------------

  async function handleDeletePet(pet: Pet) {
    const confirmed = window.confirm(
      `Are you sure you want to delete ${pet.name}?`
    );

    if (!confirmed) return;

    try {
      setError("");
      setSuccess("");

      await petService.deletePet(pet.id);
      setPets((current) => current.filter((item) => item.id !== pet.id));
      setSuccess(`${pet.name} was deleted successfully.`);
    } catch (err: any) {
      console.error(err);
      setError(err?.message || "Unable to delete the pet.");
    }
  }

  return (
    <div className="page-wrap">
      {/* -------------------------------------------------------
          PAGE HEADER
      ------------------------------------------------------- */}

      <div className="page-heading">
        <div>
          <span className="eyebrow">PET CARE MANAGEMENT</span>

          <h2>{isPetOwner ? "My Pets" : "Pets & Owners Directory"}</h2>

          <p>
            {isPetOwner
              ? "Register and manage your own pets."
              : "Register pet owners and their pets. All Owner IDs and Pet IDs are generated by the backend and saved in PostgreSQL."}
          </p>
        </div>

        <div className="heading-actions">
          {!isPetOwner && (
            <button
              type="button"
              className="btn btn-secondary"
              onClick={openAddOwnerModal}
            >
              <UserPlus size={16} />
              Register Owner
            </button>
          )}

          <button
            type="button"
            className="btn btn-primary"
            onClick={() => openAddPetModal()}
          >
            <Plus size={16} />
            Register Pet
          </button>
        </div>
      </div>

      {/* -------------------------------------------------------
          SUCCESS / ERROR NOTICES
      ------------------------------------------------------- */}

      {success && <div className="notice">{success}</div>}
      {error && !isPetModalOpen && !isOwnerModalOpen && (
        <div className="form-error">{error}</div>
      )}

      {/* -------------------------------------------------------
          FILTER BAR
      ------------------------------------------------------- */}

      <div className="filter-bar">
        <div className="search-input">
          <Search size={16} />

          <input
            type="text"
            placeholder={isPetOwner ? "Search your pets, species or breed..." : "Search pets, species, breed or owner..."}
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        </div>

        <div className="select-input">
          <SlidersHorizontal size={16} />

          <select
            value={speciesFilter}
            onChange={(event) => setSpeciesFilter(event.target.value)}
          >
            {speciesList.map((species) => (
              <option key={species} value={species}>
                {species === "All" ? "All Species" : species}
              </option>
            ))}
          </select>
        </div>

        <span className="filter-summary">
          {filteredPets.length} pet
          {filteredPets.length !== 1 ? "s" : ""}
        </span>
      </div>

      {/* -------------------------------------------------------
          PET DIRECTORY TABLE
      ------------------------------------------------------- */}

      <section className="card">
        <div className="card-header">
          <div>
            <span className="eyebrow">REGISTERED PETS</span>
            <h3>Pet Directory</h3>
          </div>

          <span className="badge badge-neutral">{pets.length} Total</span>
        </div>

        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Pet</th>
                <th>Species</th>
                <th>Breed</th>
                <th>Owner</th>
                <th>Gender</th>
                <th>Date of Birth</th>
                <th>Weight</th>
                <th>Actions</th>
              </tr>
            </thead>

            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={8}>
                    <div className="empty-state">
                      <strong>Loading pets...</strong>
                      <span>Please wait.</span>
                    </div>
                  </td>
                </tr>
              ) : filteredPets.length === 0 ? (
                <tr>
                  <td colSpan={8}>
                    <div className="empty-state">
                      <strong>
                        {pets.length === 0
                          ? "No pets registered yet"
                          : "No matching pets"}
                      </strong>

                      <span>
                        {pets.length === 0
                          ? "Register an owner and a pet to get started."
                          : "Try changing your search or filter."}
                      </span>
                    </div>
                  </td>
                </tr>
              ) : (
                filteredPets.map((pet) => {
                  const owner = ownerMap.get(pet.ownerId);

                  return (
                    <tr key={pet.id}>
                      <td>
                        <div className="person-cell">
                          <div className="person-avatar">
                            {pet.name.charAt(0).toUpperCase()}
                          </div>

                          <div>
                            <strong>{pet.name}</strong>
                            <span className="muted">ID: {pet.id}</span>
                          </div>
                        </div>
                      </td>

                      <td>
                        <strong>{pet.species}</strong>
                      </td>

                      <td>{pet.breed || "—"}</td>

                      <td>
                        <div>
                          <strong>{owner ? owner.fullName : pet.ownerId}</strong>
                          {owner && (
                            <span className="muted" style={{ display: "block", fontSize: "10px" }}>
                              {owner.id}
                            </span>
                          )}
                        </div>
                      </td>

                      <td>{pet.gender || "—"}</td>

                      <td>{formatDate(pet.dateOfBirth)}</td>

                      <td>{pet.weight ? `${pet.weight} kg` : "—"}</td>

                      <td>
                        <div
                          style={{
                            display: "flex",
                            gap: "5px",
                            alignItems: "center",
                          }}
                        >
                          <button
                            type="button"
                            className="btn btn-secondary"
                            onClick={() => openEditPetModal(pet)}
                          >
                            Edit
                          </button>

                          <button
                            type="button"
                            className="btn btn-ghost"
                            onClick={() => handleDeletePet(pet)}
                            title={`Delete ${pet.name}`}
                          >
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        <div className="info-strip">
          <PawPrint size={18} />

          <div>
            <strong>Pet ownership validation</strong>

            <span>
              Each pet is linked to an Owner ID generated by the backend. The system will validate ownership before any consultation request is created.
            </span>
          </div>
        </div>
      </section>

      {/* -------------------------------------------------------
          REGISTER PET OWNER MODAL
      ------------------------------------------------------- */}

      {isOwnerModalOpen && (
        <div
          className="modal-overlay"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closeOwnerModal();
            }
          }}
        >
          <div className="consultation-modal">
            <div className="consultation-modal-header">
              <div className="consultation-modal-title">
                <div className="consultation-modal-icon">
                  <UserPlus size={21} />
                </div>
                <div>
                  <span className="eyebrow">PET OWNER REGISTRATION</span>
                  <h3>Register New Pet Owner</h3>
                  <p>The backend will auto-generate the Owner ID (e.g. OWN-2001).</p>
                </div>
              </div>

              <button
                type="button"
                className="modal-close-button"
                onClick={closeOwnerModal}
                disabled={saving}
              >
                <X size={20} />
              </button>
            </div>

            <form className="consultation-form" onSubmit={handleOwnerSubmit}>
              <div className="form-group">
                <label htmlFor="ownerFullName">
                  Full Name <span>*</span>
                </label>
                <input
                  id="ownerFullName"
                  type="text"
                  placeholder="e.g. Miran Perera"
                  value={ownerForm.fullName}
                  onChange={(e) =>
                    setOwnerForm((c) => ({ ...c, fullName: e.target.value }))
                  }
                  disabled={saving}
                />
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="ownerEmail">
                    Email <span>*</span>
                  </label>
                  <input
                    id="ownerEmail"
                    type="email"
                    placeholder="e.g. miran@example.com"
                    value={ownerForm.email}
                    onChange={(e) =>
                      setOwnerForm((c) => ({ ...c, email: e.target.value }))
                    }
                    disabled={saving}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="ownerPhone">
                    Phone Number <span>*</span>
                  </label>
                  <input
                    id="ownerPhone"
                    type="tel"
                    placeholder="e.g. +94 77 123 4567"
                    value={ownerForm.phoneNumber}
                    onChange={(e) =>
                      setOwnerForm((c) => ({ ...c, phoneNumber: e.target.value }))
                    }
                    disabled={saving}
                  />
                </div>
              </div>

              <div className="form-group">
                <label htmlFor="ownerAddress">Address</label>
                <input
                  id="ownerAddress"
                  type="text"
                  placeholder="e.g. 123 Temple Road, Colombo"
                  value={ownerForm.address}
                  onChange={(e) =>
                    setOwnerForm((c) => ({ ...c, address: e.target.value }))
                  }
                  disabled={saving}
                />
              </div>

              {error && <div className="form-error">{error}</div>}

              <div className="consultation-modal-footer">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={closeOwnerModal}
                  disabled={saving}
                >
                  Cancel
                </button>

                <button
                  type="submit"
                  className="btn btn-primary"
                  disabled={saving}
                >
                  {saving ? "Registering..." : "Register Owner"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* -------------------------------------------------------
          REGISTER / EDIT PET MODAL
      ------------------------------------------------------- */}

      {isPetModalOpen && (
        <div
          className="modal-overlay"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) {
              closePetModal();
            }
          }}
        >
          <div className="consultation-modal">
            <div className="consultation-modal-header">
              <div className="consultation-modal-title">
                <div className="consultation-modal-icon">
                  <PawPrint size={21} />
                </div>
                <div>
                  <span className="eyebrow">
                    {editingPet ? "UPDATE PET" : "PET REGISTRATION"}
                  </span>
                  <h3>{editingPet ? "Edit Pet Details" : "Register New Pet"}</h3>
                  <p>The backend will auto-generate the Pet ID (e.g. PET-1001).</p>
                </div>
              </div>

              <button
                type="button"
                className="modal-close-button"
                onClick={closePetModal}
                disabled={saving}
              >
                <X size={20} />
              </button>
            </div>

            <form className="consultation-form" onSubmit={handlePetSubmit}>
              <div className="form-group">
                <label htmlFor="petOwnerSelect">
                  Pet Owner <span>*</span>
                </label>

                {owners.length > 0 ? (
                  <select
                    id="petOwnerSelect"
                    value={petForm.ownerId}
                    onChange={(e) =>
                      setPetForm((c) => ({ ...c, ownerId: e.target.value }))
                    }
                    disabled={saving || Boolean(editingPet) || isPetOwner}
                  >
                    <option value="">Select an Owner</option>
                    {owners.map((owner) => (
                      <option key={owner.id} value={owner.id}>
                        {owner.fullName} ({owner.id}) — {owner.email}
                      </option>
                    ))}
                  </select>
                ) : (
                  <input
                    id="petOwnerInput"
                    type="text"
                    placeholder="Enter Owner ID (e.g. OWN-2001)"
                    value={petForm.ownerId}
                    onChange={(e) =>
                      setPetForm((c) => ({ ...c, ownerId: e.target.value }))
                    }
                    disabled={saving || Boolean(editingPet)}
                  />
                )}
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="petName">
                    Pet Name <span>*</span>
                  </label>
                  <input
                    id="petName"
                    type="text"
                    placeholder="e.g. Max"
                    value={petForm.name}
                    onChange={(e) =>
                      setPetForm((c) => ({ ...c, name: e.target.value }))
                    }
                    disabled={saving}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="petSpecies">
                    Species <span>*</span>
                  </label>
                  <select
                    id="petSpecies"
                    value={petForm.species}
                    onChange={(e) =>
                      setPetForm((c) => ({ ...c, species: e.target.value }))
                    }
                    disabled={saving}
                  >
                    <option value="">Select species</option>
                    <option value="Dog">Dog</option>
                    <option value="Cat">Cat</option>
                    <option value="Bird">Bird</option>
                    <option value="Rabbit">Rabbit</option>
                    <option value="Other">Other</option>
                  </select>
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="petBreed">Breed</label>
                  <input
                    id="petBreed"
                    type="text"
                    placeholder="e.g. Golden Retriever"
                    value={petForm.breed}
                    onChange={(e) =>
                      setPetForm((c) => ({ ...c, breed: e.target.value }))
                    }
                    disabled={saving}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="petGender">Gender</label>
                  <select
                    id="petGender"
                    value={petForm.gender}
                    onChange={(e) =>
                      setPetForm((c) => ({ ...c, gender: e.target.value }))
                    }
                    disabled={saving}
                  >
                    <option value="">Select gender</option>
                    <option value="Male">Male</option>
                    <option value="Female">Female</option>
                  </select>
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="petDob">Date of Birth</label>
                  <input
                    id="petDob"
                    type="date"
                    value={petForm.dateOfBirth}
                    onChange={(e) =>
                      setPetForm((c) => ({ ...c, dateOfBirth: e.target.value }))
                    }
                    disabled={saving}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="petWeight">Weight (kg)</label>
                  <input
                    id="petWeight"
                    type="number"
                    step="0.1"
                    min="0"
                    placeholder="e.g. 12.5"
                    value={petForm.weight}
                    onChange={(e) =>
                      setPetForm((c) => ({ ...c, weight: e.target.value }))
                    }
                    disabled={saving}
                  />
                </div>
              </div>

              {error && <div className="form-error">{error}</div>}

              <div className="consultation-modal-footer">
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={closePetModal}
                  disabled={saving}
                >
                  Cancel
                </button>

                <button
                  type="submit"
                  className="btn btn-primary"
                  disabled={saving}
                >
                  {saving
                    ? "Saving..."
                    : editingPet
                      ? "Save Changes"
                      : "Register Pet"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}

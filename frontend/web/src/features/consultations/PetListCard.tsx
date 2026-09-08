import React, { useState } from "react";
import {
  Check,
  Clipboard,
  Copy,
  Edit3,
  HeartPulse,
  PawPrint,
  Plus,
  Search,
  Sparkles,
  Stethoscope,
  User,
} from "lucide-react";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import type { Pet } from "../../services/api";
import { formatDate } from "../../utils/format";

interface PetListCardProps {
  pets: Pet[];
  loading?: boolean;
  onRegisterPet: () => void;
  onRequestConsultation: (pet: Pet) => void;
  onViewHistory: (pet: Pet) => void;
  onEditPet: (pet: Pet) => void;
  selectedOwnerId?: string;
}

export const PetListCard: React.FC<PetListCardProps> = ({
  pets,
  loading = false,
  onRegisterPet,
  onRequestConsultation,
  onViewHistory,
  onEditPet,
  selectedOwnerId,
}) => {
  const [searchTerm, setSearchTerm] = useState("");
  const [speciesFilter, setSpeciesFilter] = useState("All");
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(id);
    setTimeout(() => setCopiedId(null), 1800);
  };

  const filteredPets = pets.filter((pet) => {
    const matchesSearch =
      pet.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
      pet.breed.toLowerCase().includes(searchTerm.toLowerCase()) ||
      pet.species.toLowerCase().includes(searchTerm.toLowerCase()) ||
      pet.id.toLowerCase().includes(searchTerm.toLowerCase()) ||
      pet.ownerId.toLowerCase().includes(searchTerm.toLowerCase());

    const matchesSpecies =
      speciesFilter === "All" ||
      pet.species.toLowerCase() === speciesFilter.toLowerCase();

    return matchesSearch && matchesSpecies;
  });

  const getSpeciesTone = (
    species: string
  ): "success" | "info" | "warning" | "neutral" => {
    switch (species?.toLowerCase()) {
      case "dog":
        return "info";
      case "cat":
        return "success";
      case "bird":
        return "warning";
      case "rabbit":
        return "neutral";
      default:
        return "neutral";
    }
  };

  return (
    <Card>
      <div className="card-header">
        <div>
          <div className="eyebrow">Patient Directory</div>
          <h3>Registered Patients ({pets.length})</h3>
        </div>
        <Button
          icon={<Plus size={15} />}
          onClick={onRegisterPet}
          variant="primary"
        >
          Register Pet
        </Button>
      </div>

      {/* Filter & Search Bar */}
      <div className="filter-bar" style={{ marginBottom: "16px" }}>
        <div className="search-input">
          <Search size={16} />
          <input
            placeholder="Search patient or owner..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
          />
        </div>
        <div className="select-input">
          <select
            value={speciesFilter}
            onChange={(e) => setSpeciesFilter(e.target.value)}
          >
            <option value="All">All Species</option>
            <option value="Dog">Dogs</option>
            <option value="Cat">Cats</option>
            <option value="Bird">Birds</option>
            <option value="Rabbit">Rabbits</option>
            <option value="Other">Other</option>
          </select>
        </div>
        <div className="filter-summary">
          {filteredPets.length} of {pets.length} pet{pets.length === 1 ? "" : "s"}
        </div>
      </div>

      {/* Loading / Empty State / Grid */}
      {loading ? (
        <div className="empty-state" style={{ minHeight: "200px" }}>
          <PawPrint size={32} className="animate-pulse" style={{ color: "var(--primary)" }} />
          <p style={{ marginTop: "12px" }}>Loading pet profiles from server...</p>
        </div>
      ) : filteredPets.length === 0 ? (
        <div className="empty-state" style={{ minHeight: "220px" }}>
          <PawPrint size={36} style={{ color: "var(--muted)", opacity: 0.5 }} />
          <h4 style={{ margin: "10px 0 4px", fontSize: "14px" }}>
            {pets.length === 0
              ? selectedOwnerId
                ? `No pets registered under ${selectedOwnerId} yet.`
                : "No pets registered in the clinic database yet."
              : "No pets match your filter criteria."}
          </h4>
          <p style={{ margin: 0, fontSize: "11px", color: "var(--muted)", maxWidth: "380px" }}>
            {pets.length === 0
              ? "Register a new pet with short ID generation to enable consultation requests and medical records."
              : "Try clearing search filters or changing the species filter."}
          </p>
          {pets.length === 0 && (
            <Button
              onClick={onRegisterPet}
              style={{ marginTop: "14px" }}
              icon={<Plus size={15} />}
            >
              Register First Pet
            </Button>
          )}
        </div>
      ) : (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(310px, 1fr))",
            gap: "14px",
          }}
        >
          {filteredPets.map((pet) => (
            <div
              key={pet.id}
              style={{
                border: "1px solid var(--line)",
                borderRadius: "14px",
                padding: "14px",
                background: "#fbfdfc",
                display: "flex",
                flexDirection: "column",
                justifyContent: "space-between",
                gap: "12px",
                transition: "all 0.15s ease",
              }}
            >
              {/* Pet Card Header */}
              <div>
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "flex-start",
                    gap: "8px",
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                    {pet.photoUrl ? (
                      <img
                        src={pet.photoUrl}
                        alt={pet.name}
                        onError={(e) => ((e.target as HTMLElement).style.display = "none")}
                        style={{
                          width: "42px",
                          height: "42px",
                          borderRadius: "11px",
                          objectFit: "cover",
                          flexShrink: 0,
                        }}
                      />
                    ) : (
                      <div
                        style={{
                          width: "42px",
                          height: "42px",
                          borderRadius: "11px",
                          background: "var(--primary-soft)",
                          color: "var(--primary)",
                          display: "grid",
                          placeItems: "center",
                          flexShrink: 0,
                        }}
                      >
                        <PawPrint size={20} />
                      </div>
                    )}
                    <div>
                      <strong style={{ fontSize: "15px", color: "var(--ink)" }}>
                        {pet.name}
                      </strong>
                      <span
                        style={{
                          display: "block",
                          fontSize: "11px",
                          color: "var(--muted)",
                        }}
                      >
                        {pet.breed} · {pet.age} yr{pet.age === 1 ? "" : "s"} old
                      </span>
                    </div>
                  </div>
                  <Badge tone={getSpeciesTone(pet.species)}>{pet.species}</Badge>
                </div>

                {/* Date of Birth & Short IDs Info Bar */}
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    marginTop: "10px",
                    padding: "6px 8px",
                    background: "#f4f8f6",
                    borderRadius: "8px",
                    fontSize: "10px",
                    color: "var(--muted)",
                  }}
                >
                  <span>
                    DOB: {pet.dateOfBirth ? formatDate(pet.dateOfBirth.slice(0, 10)) : "Not specified"}
                  </span>
                  <span>
                    Owner: <strong style={{ color: "var(--ink)" }}>{pet.ownerId}</strong>
                  </span>
                </div>

                {/* Notes / Health Summary Preview */}
                <div
                  style={{
                    marginTop: "8px",
                    padding: "8px 10px",
                    borderRadius: "8px",
                    background: "#fff",
                    border: "1px solid var(--line)",
                    fontSize: "11px",
                    color: "#465751",
                  }}
                >
                  <span
                    style={{
                      display: "block",
                      fontSize: "9px",
                      textTransform: "uppercase",
                      letterSpacing: "0.05em",
                      fontWeight: 800,
                      color: "var(--muted)",
                      marginBottom: "2px",
                    }}
                  >
                    Notes & Profile Summary
                  </span>
                  <span
                    style={{
                      display: "-webkit-box",
                      WebkitLineClamp: 2,
                      WebkitBoxOrient: "vertical",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      lineHeight: "1.4",
                    }}
                  >
                    {pet.notes || pet.medicalHistorySummary || "General wellness up to date. No known food or drug allergies."}
                  </span>
                </div>
              </div>

              {/* Card Footer: Short ID & Actions */}
              <div
                style={{
                  borderTop: "1px solid var(--line)",
                  paddingTop: "10px",
                  display: "flex",
                  flexDirection: "column",
                  gap: "8px",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    fontSize: "10px",
                    color: "var(--muted)",
                  }}
                >
                  <span>
                    Pet ID: <code style={{ color: "var(--ink)", fontWeight: 700 }}>{pet.id}</code>
                  </span>
                  <button
                    type="button"
                    onClick={() => copyToClipboard(pet.id, `pet-${pet.id}`)}
                    style={{
                      background: "none",
                      border: "none",
                      color: copiedId === `pet-${pet.id}` ? "var(--success)" : "var(--primary)",
                      cursor: "pointer",
                      display: "flex",
                      alignItems: "center",
                      gap: "3px",
                      fontSize: "10px",
                    }}
                    title="Copy Pet Short ID"
                  >
                    {copiedId === `pet-${pet.id}` ? (
                      <>
                        <Check size={11} /> Copied
                      </>
                    ) : (
                      <>
                        <Copy size={11} /> Copy ID
                      </>
                    )}
                  </button>
                </div>

                {/* Primary & Secondary Action Buttons */}
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "6px" }}>
                  <Button
                    variant="secondary"
                    onClick={() => onViewHistory(pet)}
                    icon={<Stethoscope size={13} />}
                    style={{ fontSize: "10px", padding: "4px 8px", minHeight: "32px", justifyContent: "center" }}
                    title="View medical diagnoses and vaccination history"
                  >
                    Clinical Log
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={() => onEditPet(pet)}
                    icon={<Edit3 size={13} />}
                    style={{ fontSize: "10px", padding: "4px 8px", minHeight: "32px", justifyContent: "center" }}
                    title="Edit patient profile details"
                  >
                    Edit Profile
                  </Button>
                </div>

                <Button
                  variant="primary"
                  onClick={() => onRequestConsultation(pet)}
                  icon={<Sparkles size={14} />}
                  style={{
                    width: "100%",
                    justifyContent: "center",
                    fontSize: "11px",
                    minHeight: "34px",
                  }}
                >
                  Request Consultation
                </Button>
              </div>
            </div>
          ))}
        </div>
      )}
    </Card>
  );
};

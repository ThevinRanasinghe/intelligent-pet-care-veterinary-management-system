import React, { useState } from "react";
import {
  Check,
  Clipboard,
  Copy,
  Dog,
  Cat,
  Bird,
  HeartPulse,
  PawPrint,
  Plus,
  Search,
  Sparkles,
  User,
} from "lucide-react";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import type { Pet } from "../../services/api";

interface PetListCardProps {
  pets: Pet[];
  loading?: boolean;
  onRegisterPet: () => void;
  onRequestConsultation: (pet: Pet) => void;
  selectedOwnerId?: string;
}

export const PetListCard: React.FC<PetListCardProps> = ({
  pets,
  loading = false,
  onRegisterPet,
  onRequestConsultation,
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
    switch (species.toLowerCase()) {
      case "dog":
        return "info";
      case "cat":
        return "success";
      case "bird":
        return "warning";
      default:
        return "neutral";
    }
  };

  return (
    <Card>
      <div className="card-header">
        <div>
          <div className="eyebrow">Registered Patients</div>
          <h3>Pet Profiles ({pets.length})</h3>
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
            placeholder="Search by pet name, breed, or ID..."
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

      {/* Loading Skeleton / Empty State / Pet Cards Grid */}
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
                ? "No pets registered under this owner yet."
                : "No pets registered yet in the system."
              : "No pets match your search criteria."}
          </h4>
          <p style={{ margin: 0, fontSize: "11px", color: "var(--muted)", maxWidth: "360px" }}>
            {pets.length === 0
              ? "Register a new pet to begin creating consultation requests and medical records."
              : "Try clearing filters or changing your search term."}
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
            gridTemplateColumns: "repeat(auto-fill, minmax(290px, 1fr))",
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
                    <div
                      style={{
                        width: "36px",
                        height: "36px",
                        borderRadius: "10px",
                        background: "var(--primary-soft)",
                        color: "var(--primary)",
                        display: "grid",
                        placeItems: "center",
                        flexShrink: 0,
                      }}
                    >
                      <PawPrint size={18} />
                    </div>
                    <div>
                      <strong style={{ fontSize: "14px", color: "var(--ink)" }}>
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

                {/* Medical History */}
                <div
                  style={{
                    marginTop: "10px",
                    padding: "8px 10px",
                    borderRadius: "8px",
                    background: "#f4f8f6",
                    border: "1px solid #e2ebe6",
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
                    Medical History
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
                    {pet.medicalHistorySummary || "No previous conditions recorded. General checkup up-to-date."}
                  </span>
                </div>
              </div>

              {/* GUID and Quick Actions */}
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
                    Pet ID: <code style={{ color: "var(--ink)", fontWeight: 600 }}>{pet.id.slice(0, 8)}...</code>
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
                    title="Copy full Pet GUID"
                  >
                    {copiedId === `pet-${pet.id}` ? (
                      <>
                        <Check size={11} /> Copied
                      </>
                    ) : (
                      <>
                        <Copy size={11} /> Copy GUID
                      </>
                    )}
                  </button>
                </div>

                <Button
                  variant="secondary"
                  onClick={() => onRequestConsultation(pet)}
                  icon={<Sparkles size={14} />}
                  style={{
                    width: "100%",
                    justifyContent: "center",
                    fontSize: "11px",
                    minHeight: "32px",
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

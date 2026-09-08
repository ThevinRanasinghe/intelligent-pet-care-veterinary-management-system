import React, { useEffect, useState } from "react";
import {
  Activity,
  AlertCircle,
  CheckCircle2,
  Filter,
  PawPrint,
  Plus,
  RefreshCw,
  Sparkles,
  Stethoscope,
  User,
  Users,
  Wifi,
  WifiOff,
} from "lucide-react";
import { Badge } from "../../components/ui/Badge";
import { Button } from "../../components/ui/Button";
import { Card } from "../../components/ui/Card";
import { ConsultationModal } from "../../components/ConsultationModal";
import { PetManagementModal } from "./PetManagementModal";
import { PetHistoryModal } from "./PetHistoryModal";
import { PetListCard } from "./PetListCard";
import { ConsultationStatusTracker } from "./ConsultationStatusTracker";
import {
  API_BASE_URL,
  DEMO_OWNERS,
  petService,
  consultationService,
  type ConsultationRequest,
  type Pet,
} from "../../services/api";

export function ConsultationsPage() {
  const [activeTab, setActiveTab] = useState<"pets" | "consultations">("pets");
  const [selectedOwnerId, setSelectedOwnerId] = useState<string>(DEMO_OWNERS[0].id);
  const [viewAllOwners, setViewAllOwners] = useState<boolean>(false);

  // Data state
  const [pets, setPets] = useState<Pet[]>([]);
  const [consultations, setConsultations] = useState<ConsultationRequest[]>([]);
  const [loadingPets, setLoadingPets] = useState<boolean>(false);
  const [loadingConsultations, setLoadingConsultations] = useState<boolean>(false);
  const [backendOnline, setBackendOnline] = useState<boolean | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  // Modal triggers
  const [isPetModalOpen, setIsPetModalOpen] = useState<boolean>(false);
  const [petToEdit, setPetToEdit] = useState<Pet | null>(null);
  const [isHistoryModalOpen, setIsHistoryModalOpen] = useState<boolean>(false);
  const [selectedPetForHistory, setSelectedPetForHistory] = useState<Pet | null>(null);
  const [isConsultationModalOpen, setIsConsultationModalOpen] = useState<boolean>(false);
  const [selectedPetForConsultation, setSelectedPetForConsultation] = useState<Pet | null>(null);

  // Fetch pets
  const loadPets = async () => {
    setLoadingPets(true);
    setErrorMessage(null);
    try {
      let data: Pet[];
      if (viewAllOwners) {
        data = await petService.getAllPets();
      } else {
        data = await petService.getPetsByOwner(selectedOwnerId);
      }
      setPets(data);
      setBackendOnline(true);
    } catch (err: any) {
      setBackendOnline(false);
      setErrorMessage(err.message || "Failed to load pets from server.");
    } finally {
      setLoadingPets(false);
    }
  };

  // Fetch consultations
  const loadConsultations = async () => {
    setLoadingConsultations(true);
    try {
      let data: ConsultationRequest[];
      if (viewAllOwners) {
        data = await consultationService.getAllConsultations();
      } else {
        data = await consultationService.getConsultationsByOwner(selectedOwnerId);
      }
      setConsultations(data);
      setBackendOnline(true);
    } catch (err: any) {
      // Handled gracefully in UI
      setBackendOnline(false);
    } finally {
      setLoadingConsultations(false);
    }
  };

  useEffect(() => {
    loadPets();
    loadConsultations();
  }, [selectedOwnerId, viewAllOwners]);

  const handlePetSaved = (savedPet: Pet) => {
    setPets((prev) => {
      const idx = prev.findIndex((p) => p.id === savedPet.id);
      if (idx >= 0) {
        const copy = [...prev];
        copy[idx] = savedPet;
        return copy;
      }
      return [savedPet, ...prev];
    });
  };

  const handleConsultationCreated = (newConsultation: ConsultationRequest) => {
    setConsultations((prev) => [newConsultation, ...prev]);
    setActiveTab("consultations");
  };

  const handleRequestConsultationForPet = (pet: Pet) => {
    setSelectedPetForConsultation(pet);
    setIsConsultationModalOpen(true);
  };

  const handleViewHistoryForPet = (pet: Pet) => {
    setSelectedPetForHistory(pet);
    setIsHistoryModalOpen(true);
  };

  const handleEditPet = (pet: Pet) => {
    setPetToEdit(pet);
    setIsPetModalOpen(true);
  };

  const handleOpenNewPetModal = () => {
    setPetToEdit(null);
    setIsPetModalOpen(true);
  };

  return (
    <div className="page-wrap">
      {/* Page Heading */}
      <div className="page-heading">
        <div>
          <div className="eyebrow">Patient Care · Intake & Records</div>
          <h2>Patient Intake & Consultation Center</h2>
          <p>
            Overview of registered pets, active patient records, and consultation intake requests.
          </p>
        </div>
        <div className="heading-actions" style={{ display: "flex", gap: "8px", flexWrap: "wrap" }}>
          <Button
            variant="secondary"
            icon={<PawPrint size={16} />}
            onClick={handleOpenNewPetModal}
          >
            Register Pet
          </Button>
          <Button
            variant="primary"
            icon={<Sparkles size={16} />}
            onClick={() => {
              setSelectedPetForConsultation(null);
              setIsConsultationModalOpen(true);
            }}
          >
            New Consultation
          </Button>
        </div>
      </div>

      {/* Connectivity Status & Owner Filter Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "12px",
          marginBottom: "18px",
          background: "#fff",
          padding: "12px 16px",
          borderRadius: "14px",
          border: "1px solid var(--line)",
        }}
      >
        {/* Owner Selector */}
        <div style={{ display: "flex", alignItems: "center", gap: "8px", flexWrap: "wrap" }}>
          <User size={16} style={{ color: "var(--muted)" }} />
          <span style={{ fontSize: "12px", fontWeight: 700, color: "var(--ink)" }}>Filter by Owner:</span>
          <div style={{ display: "flex", gap: "6px", flexWrap: "wrap" }}>
            <button
              type="button"
              onClick={() => setViewAllOwners(true)}
              style={{
                fontSize: "11px",
                padding: "5px 12px",
                borderRadius: "8px",
                border: "1px solid var(--line)",
                background: viewAllOwners ? "var(--primary-soft)" : "#fff",
                color: viewAllOwners ? "var(--primary-deep)" : "var(--ink)",
                fontWeight: viewAllOwners ? 700 : 500,
                cursor: "pointer",
                display: "flex",
                alignItems: "center",
                gap: "5px",
                transition: "all 0.15s ease",
              }}
            >
              <Users size={12} /> All Patients
            </button>
            {DEMO_OWNERS.map((owner) => (
              <button
                key={owner.id}
                type="button"
                onClick={() => {
                  setSelectedOwnerId(owner.id);
                  setViewAllOwners(false);
                }}
                style={{
                  fontSize: "11px",
                  padding: "5px 12px",
                  borderRadius: "8px",
                  border: "1px solid var(--line)",
                  background:
                    !viewAllOwners && selectedOwnerId.toUpperCase() === owner.id.toUpperCase()
                      ? "var(--primary-soft)"
                      : "#fff",
                  color:
                    !viewAllOwners && selectedOwnerId.toUpperCase() === owner.id.toUpperCase()
                      ? "var(--primary-deep)"
                      : "var(--ink)",
                  fontWeight: !viewAllOwners && selectedOwnerId.toUpperCase() === owner.id.toUpperCase() ? 700 : 500,
                  cursor: "pointer",
                  transition: "all 0.15s ease",
                }}
              >
                {owner.fullName}
              </button>
            ))}
          </div>
        </div>

        {/* Backend Connectivity Badge */}
        <div style={{ display: "flex", alignItems: "center", gap: "8px" }}>
          {backendOnline === true ? (
            <Badge tone="success">
              <span style={{ width: "7px", height: "7px", borderRadius: "99px", background: "#45a978", display: "inline-block", marginRight: "6px" }} />
              System Online
            </Badge>
          ) : backendOnline === false ? (
            <Badge tone="warning">
              <span style={{ width: "7px", height: "7px", borderRadius: "99px", background: "#ea580c", display: "inline-block", marginRight: "6px" }} />
              Offline Mode
            </Badge>
          ) : (
            <Badge tone="neutral">Connecting...</Badge>
          )}
          <Button
            variant="ghost"
            onClick={() => {
              loadPets();
              loadConsultations();
            }}
            icon={<RefreshCw size={13} className={loadingPets ? "animate-spin" : ""} />}
            title="Refresh from server"
          />
        </div>
      </div>

      {/* Backend Offline Banner */}
      {backendOnline === false && (
        <div
          className="form-error"
          style={{
            marginBottom: "18px",
            background: "#fff9f2",
            borderColor: "#fed7aa",
            color: "#9a3412",
          }}
        >
          <div style={{ display: "flex", gap: "10px", alignItems: "flex-start" }}>
            <AlertCircle size={18} style={{ color: "#ea580c", flexShrink: 0, marginTop: "2px" }} />
            <div>
              <strong>Backend Service Unavailable</strong>
              <p style={{ margin: "3px 0 0", fontSize: "11px", lineHeight: "1.4" }}>
                The clinical service API is currently unreachable. Displaying cached records and local validation mode.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Navigation Tabs */}
      <div
        style={{
          display: "flex",
          gap: "8px",
          borderBottom: "1px solid var(--line)",
          marginBottom: "18px",
        }}
      >
        <button
          type="button"
          onClick={() => setActiveTab("pets")}
          style={{
            padding: "10px 16px",
            border: "none",
            borderBottom: activeTab === "pets" ? "3px solid var(--primary)" : "3px solid transparent",
            background: "none",
            fontSize: "13px",
            fontWeight: 700,
            color: activeTab === "pets" ? "var(--primary-deep)" : "var(--muted)",
            cursor: "pointer",
            display: "flex",
            alignItems: "center",
            gap: "7px",
          }}
        >
          <PawPrint size={16} />
          Pet Profiles ({pets.length})
        </button>
        <button
          type="button"
          onClick={() => setActiveTab("consultations")}
          style={{
            padding: "10px 16px",
            border: "none",
            borderBottom: activeTab === "consultations" ? "3px solid var(--primary)" : "3px solid transparent",
            background: "none",
            fontSize: "13px",
            fontWeight: 700,
            color: activeTab === "consultations" ? "var(--primary-deep)" : "var(--muted)",
            cursor: "pointer",
            display: "flex",
            alignItems: "center",
            gap: "7px",
          }}
        >
          <Activity size={16} />
          Consultation Requests & Tracker ({consultations.length})
        </button>
      </div>

      {/* Tab Content */}
      {activeTab === "pets" ? (
        <PetListCard
          pets={pets}
          loading={loadingPets}
          onRegisterPet={handleOpenNewPetModal}
          onRequestConsultation={handleRequestConsultationForPet}
          onViewHistory={handleViewHistoryForPet}
          onEditPet={handleEditPet}
          selectedOwnerId={viewAllOwners ? undefined : selectedOwnerId}
        />
      ) : (
        <ConsultationStatusTracker
          consultations={consultations}
          loading={loadingConsultations}
          onRefresh={loadConsultations}
          onNewConsultation={() => {
            setSelectedPetForConsultation(null);
            setIsConsultationModalOpen(true);
          }}
        />
      )}

      {/* Modals */}
      <PetManagementModal
        isOpen={isPetModalOpen}
        onClose={() => {
          setIsPetModalOpen(false);
          setPetToEdit(null);
        }}
        onSuccess={handlePetSaved}
        initialOwnerId={viewAllOwners ? undefined : selectedOwnerId}
        petToEdit={petToEdit}
      />

      <PetHistoryModal
        isOpen={isHistoryModalOpen}
        onClose={() => {
          setIsHistoryModalOpen(false);
          setSelectedPetForHistory(null);
        }}
        pet={selectedPetForHistory}
        onRecordAdded={loadPets}
      />

      <ConsultationModal
        isOpen={isConsultationModalOpen}
        onClose={() => {
          setIsConsultationModalOpen(false);
          setSelectedPetForConsultation(null);
        }}
        onSuccess={handleConsultationCreated}
        initialOwnerId={selectedPetForConsultation?.ownerId || (viewAllOwners ? undefined : selectedOwnerId)}
        initialPetId={selectedPetForConsultation?.id}
        availablePets={pets}
      />
    </div>
  );
}

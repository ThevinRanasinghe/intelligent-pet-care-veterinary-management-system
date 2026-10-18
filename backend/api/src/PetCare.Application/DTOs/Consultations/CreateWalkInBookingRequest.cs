using PetCare.Application.DTOs.PetOwners;
using PetCare.Application.DTOs.Pets;
using PetCare.Application.DTOs.Scheduling;

namespace PetCare.Application.DTOs.Consultations;

/// <summary>
/// ClinicManager/Admin walk-in intake. The front desk resolves or registers
/// the owner and pet, then files a consultation request, submits it and
/// assigns the veterinarian — the same records an online booking produces,
/// marked Source = "WalkIn". Exactly one of OwnerId/NewOwner and exactly one
/// of PetId/NewPet must be supplied.
/// </summary>
public class CreateWalkInBookingRequest
{
    /// <summary>Existing owner profile id — mutually exclusive with NewOwner.</summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// New owner registration details. When an owner with the same email
    /// already exists, that record is reused instead of creating a duplicate.
    /// </summary>
    public CreatePetOwnerDto? NewOwner { get; set; }

    /// <summary>Existing pet id — mutually exclusive with NewPet.</summary>
    public string? PetId { get; set; }

    /// <summary>
    /// New pet registration details. NewPet.OwnerId is ignored — it is always
    /// replaced by the resolved owner so a walk-in can never file a pet under
    /// the wrong owner.
    /// </summary>
    public CreatePetDto? NewPet { get; set; }

    public string Symptoms { get; set; } = string.Empty;

    public string Urgency { get; set; } = "Medium";

    public decimal? Budget { get; set; }

    /// <summary>
    /// Stored on the consultation (AdditionalNotes/StatusNotes) and carried
    /// into the appointment's Notes.
    /// </summary>
    public string? AdditionalNotes { get; set; }

    /// <summary>
    /// Clinic the walk-in is booked at. Omitted for organization-scoped
    /// staff — their own clinic applies; platform admins must supply it.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    public Guid VeterinarianId { get; set; }

    /// <summary>Appointment date (YYYY-MM-DD).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Hour-aligned one-hour slot start (09:00–17:00).</summary>
    public TimeOnly StartTime { get; set; }
}

/// <summary>
/// Everything the walk-in intake persisted: the confirmed consultation with
/// its linked appointment, plus the resolved owner and pet records.
/// </summary>
public class WalkInBookingResponse
{
    public ConsultationRequestDto Consultation { get; set; } = new();

    public AppointmentResponse Appointment { get; set; } = new();

    public PetOwnerDto Owner { get; set; } = new();

    public PetDto Pet { get; set; } = new();
}

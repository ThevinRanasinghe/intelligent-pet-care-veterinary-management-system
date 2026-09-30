using PetCare.Application.DTOs.Billing;
using PetCare.Application.DTOs.Scheduling;

namespace PetCare.Application.DTOs.Manager;

/// <summary>
/// ClinicManager request to create a staff account (Veterinarian or
/// InventoryOfficer) inside their own organization. The role is assigned
/// by the endpoint and the organization is resolved from the authenticated
/// caller's tenant context — the request carries no OrganizationId, so a
/// client can never steer an account into another organization.
/// </summary>
public record ManagerCreateStaffRequest
{
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
}

/// <summary>
/// Newly created staff account. TemporaryPassword is returned exactly once
/// so the ClinicManager can hand it to the staff member — the project has
/// no email/SMS delivery mechanism, so this one-time value is the
/// documented development/demo handoff channel. The account must change it
/// on first login (MustChangePassword = true).
/// </summary>
public record CreatedStaffAccountResponse
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public bool Active { get; init; }
    public bool MustChangePassword { get; init; }
    public Guid? OrganizationId { get; init; }
    public string? OrganizationName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string TemporaryPassword { get; init; } = string.Empty;
}

/// <summary>Veterinarian row in the ClinicManager's organization list.</summary>
public record ManagerVeterinarianResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Specialisation { get; init; } = string.Empty;
    public string Branch { get; init; } = string.Empty;
    public bool Active { get; init; }
    public Guid? UserId { get; init; }
}

/// <summary>
/// Work history for one veterinarian, built for the ClinicManager dashboard.
/// Plain counts — no scoring or analytics.
/// </summary>
public record VeterinarianHistoryResponse
{
    public VeterinarianHistoryVet Veterinarian { get; init; } = new();
    public VeterinarianHistoryAppointments Appointments { get; init; } = new();
    public VeterinarianHistoryExaminations Examinations { get; init; } = new();
    public VeterinarianHistoryPrescriptions Prescriptions { get; init; } = new();
    public VeterinarianHistoryMedicineRequests MedicineRequests { get; init; } = new();
    public VeterinarianHistoryBills Bills { get; init; } = new();
}

public record VeterinarianHistoryVet
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Specialisation { get; init; } = string.Empty;
}

public record VeterinarianHistoryAppointments
{
    public int CompletedCount { get; init; }
    public int UpcomingCount { get; init; }
    public int CancelledCount { get; init; }
    public List<AppointmentResponse> Items { get; init; } = new();
}

public record VeterinarianHistoryExaminations
{
    public int Total { get; init; }
    public int InitialCount { get; init; }
    public int FollowUpCount { get; init; }
    public List<ExaminationResponseDto> Items { get; init; } = new();
}

public record VeterinarianHistoryPrescriptions
{
    public int Total { get; init; }
    public List<PrescriptionResponseDto> Items { get; init; } = new();
}

public record VeterinarianHistoryMedicineRequests
{
    public int Pending { get; init; }
    public int Issued { get; init; }
    public int Unavailable { get; init; }
}

public record VeterinarianHistoryBills
{
    public int Total { get; init; }
    public int PaidCount { get; init; }
    public int PendingCount { get; init; }
    public decimal VetChargeTotal { get; init; }
    public decimal MedicineTotal { get; init; }
    public decimal GrandTotal { get; init; }
    public List<QuotationResponse> Items { get; init; } = new();
}

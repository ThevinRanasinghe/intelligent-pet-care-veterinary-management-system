namespace PetCare.Domain.Enums;

public enum ConsultationStatus
{
    Draft,
    Submitted,
    Processing,
    PendingApproval,
    Approved,
    Rejected,
    RevisionRequired,
    AppointmentConfirmed,
    Cancelled
}
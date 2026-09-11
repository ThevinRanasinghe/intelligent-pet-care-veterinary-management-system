namespace PetCare.Domain.Constants;

/// <summary>
/// Canonical role names for the shared authentication system. Use these
/// constants instead of hardcoded strings so every module (Scheduling,
/// Billing, Approval, ...) agrees on the same role naming convention.
/// </summary>
public static class Roles
{
    /// <summary>
    /// Authorized to approve/reject/request revisions on quotations. See
    /// docs/database/scheduling-billing-approval-domain-model.md#approval.
    /// </summary>
    public const string ClinicManager = "ClinicManager";

    /// <summary>General authenticated staff member (receptionist, vet tech, etc.).</summary>
    public const string Staff = "Staff";
}
